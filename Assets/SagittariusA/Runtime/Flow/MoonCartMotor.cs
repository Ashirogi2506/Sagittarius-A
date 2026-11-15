using System;
using System.Collections.Generic;
using UnityEngine;

namespace SagittariusA.Gameplay.Flow
{
    // Kinematic four-wheel-steer cargo rover. The route is rounded in world space;
    // heading follows its tangent, while the suspension samples all four contacts.
    public sealed class MoonCartMotor
    {
        private readonly Transform root;
        private readonly Collider terrain;
        private readonly Vector3[] contacts = new Vector3[4];
        private readonly Transform[] steering = new Transform[4], rolling = new Transform[4];
        private readonly float[] radii = new float[4];
        private readonly Vector3[] ground = new Vector3[4];
        private readonly Func<Vector3, bool> isClear;
        private readonly List<Vector3> path = new List<Vector3>();
        private int segment;
        private float speed, yaw, travelled, routeLength;
        public bool Moving { get; private set; }
        public float Speed => speed;
        public float Progress => routeLength <= .01f ? 1f : Mathf.Clamp01(travelled / routeLength);
        public Vector3 Forward => Quaternion.Euler(0, yaw, 0) * Vector3.forward;
#if UNITY_EDITOR
        // Lets route checks measure actual translation/heading at matching
        // instants even when one rendered frame contains many motor steps.
        public event Action SubstepCompleted;
#endif

        public MoonCartMotor(Transform root, Collider terrain, Renderer[] wheels, Func<Vector3, bool> isClear)
        {
            this.root = root; this.terrain = terrain; this.isClear = isClear; yaw = root.eulerAngles.y;
            for (int i = 0; i < wheels.Length && i < 4; i++)
            {
                Vector3 centre = wheels[i].bounds.center;
                radii[i] = Mathf.Max(.1f, wheels[i].bounds.extents.y);
                contacts[i] = root.InverseTransformPoint(centre - Vector3.up * radii[i]);
                steering[i] = new GameObject("Wheel steering " + i).transform;
                steering[i].SetParent(root, false); steering[i].position = centre;
                rolling[i] = new GameObject("Wheel rotation " + i).transform;
                rolling[i].SetParent(steering[i], false);
                wheels[i].transform.SetParent(rolling[i], true);
            }
        }

        public void Begin(Vector3[] stops)
        {
            path.Clear(); path.Add(Flat(root.position));
            var anchors = new List<Vector3> { Flat(root.position) };
            foreach (var stop in stops)
                if (Vector3.Distance(anchors[anchors.Count - 1], Flat(stop)) > .1f) anchors.Add(Flat(stop));
            // A small rounding radius keeps the rover inside the surveyed corridor.
            for (int i = 1; i < anchors.Count - 1; i++)
            {
                Vector3 a = anchors[i - 1], b = anchors[i], c = anchors[i + 1];
                float radius = Mathf.Min(2.5f, Vector3.Distance(a, b) * .22f, Vector3.Distance(b, c) * .22f);
                Vector3 enter = b + (a - b).normalized * radius, leave = b + (c - b).normalized * radius;
                bool clearCorner = true;
                for (int n = 0; n <= 12; n++)
                {
                    float t = n / 12f;
                    if (isClear != null && !isClear((1-t)*(1-t)*enter + 2*(1-t)*t*b + t*t*leave)) { clearCorner = false; break; }
                }
                if (!clearCorner) { path.Add(b); continue; }
                path.Add(enter);
                for (int n = 1; n <= 12; n++)
                {
                    float t = n / 12f;
                    path.Add((1 - t) * (1 - t) * enter + 2 * (1 - t) * t * b + t * t * leave);
                }
            }
            if (anchors.Count > 1) path.Add(anchors[anchors.Count - 1]);
            segment = 1; speed = travelled = routeLength = 0;
            for (int i = 1; i < path.Count; i++) routeLength += Vector3.Distance(path[i - 1], path[i]);
            Moving = path.Count > 1;
        }

        public void Tick(float deltaTime)
        {
            // Substeps keep acceleration, cornering and wheel animation stable at
            // low frame rates and in accelerated editor checks.
            float remaining = Mathf.Min(deltaTime, 1f);
            while (Moving && remaining > 0)
            {
                float dt = Mathf.Min(remaining, 1f / 60f); remaining -= dt;
                Vector3 current = Flat(root.position), toward = path[segment] - current;
                if (toward.magnitude < .015f)
                {
                    segment++;
                    if (segment >= path.Count) { Moving = false; speed = 0; break; }
                    toward = path[segment] - current;
                }
                float desiredYaw = Mathf.Atan2(toward.x, toward.z) * Mathf.Rad2Deg;
                float turn = Mathf.DeltaAngle(yaw, desiredYaw);
                // Reorient before departure; never translate sideways while turning.
                float targetSpeed = Mathf.Abs(turn) > 7f ? 0 : Mathf.Min(6f, Mathf.Sqrt(Mathf.Max(0, routeLength - travelled) * 5f));
                if (segment + 1 < path.Count)
                {
                    float bend = Vector3.Angle(toward, path[segment + 1] - path[segment]);
                    targetSpeed = Mathf.Min(targetSpeed, Mathf.Lerp(6f, 1.6f, Mathf.Clamp01(bend / 25f)));
                }
                speed = Mathf.MoveTowards(speed, targetSpeed, (targetSpeed < speed ? 7f : 2.2f) * dt);
                yaw = Mathf.MoveTowardsAngle(yaw, desiredYaw, 95f * dt);
                float distance = Mathf.Abs(Mathf.DeltaAngle(yaw, desiredYaw)) < 1f ? Mathf.Min(speed * dt, toward.magnitude) : 0;
                Vector3 next = current + toward.normalized * distance;
                travelled += distance;
                PlaceOnGround(next, dt);
#if UNITY_EDITOR
                SubstepCompleted?.Invoke();
#endif
                for (int i = 0; i < 4; i++)
                {
                    if (steering[i] == null) continue;
                    float steer = Mathf.Clamp(turn * 2f, -28, 28) * (i < 2 ? 1 : -1);
                    steering[i].localRotation = Quaternion.Slerp(steering[i].localRotation, Quaternion.Euler(0, steer, 0), 8f * dt);
                    rolling[i].Rotate(Vector3.right, distance / radii[i] * Mathf.Rad2Deg, Space.Self);
                }
            }
            if (!Moving)
                for (int i = 0; i < steering.Length; i++) if (steering[i] != null) steering[i].localRotation = Quaternion.identity;
        }

        private void PlaceOnGround(Vector3 position, float dt)
        {
            Quaternion heading = Quaternion.Euler(0, yaw, 0);
            for (int i = 0; i < 4; i++)
            {
                ground[i] = position + heading * contacts[i];
                ground[i].y = Height(ground[i]);
            }
            Vector3 front = (ground[0] + ground[1]) * .5f, rear = (ground[2] + ground[3]) * .5f;
            Vector3 right = (ground[1] + ground[3] - ground[0] - ground[2]).normalized;
            Vector3 normal = Vector3.Cross(front - rear, right).normalized;
            if (normal.y < 0) normal = -normal;
            if (normal.sqrMagnitude < .5f) normal = Vector3.up;
            normal = Vector3.RotateTowards(Vector3.up, normal, 18f * Mathf.Deg2Rad, 0);
            Quaternion pose = Quaternion.LookRotation(Vector3.ProjectOnPlane(heading * Vector3.forward, normal), normal);
            // Smooth pitch/roll, but heading remains tied to the path tangent.
            Vector3 smoothUp = Vector3.Slerp(root.up, pose * Vector3.up, 8f * dt);
            root.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(heading * Vector3.forward, smoothUp), smoothUp);
            float y = float.NegativeInfinity;
            for (int i = 0; i < 4; i++) y = Mathf.Max(y, ground[i].y - (root.rotation * contacts[i]).y);
            position.y = y + .015f;
            root.position = position;
        }

        private float Height(Vector3 position)
        { return terrain.Raycast(new Ray(new Vector3(position.x, 100, position.z), Vector3.down), out var hit, 200) ? hit.point.y : root.position.y; }
        private static Vector3 Flat(Vector3 value) { value.y = 0; return value; }
    }
}
