using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace MissionSimulation.FarSignal
{
    [Serializable] public sealed class AppliedCampaignOperation
    {
        public string id, actionKey;
    }
    [Serializable] public sealed class CampaignSaveEnvelope
    {
        public int version = 1, revision;
        public CampaignState state;
        public List<AppliedCampaignOperation> operations = new List<AppliedCampaignOperation>();
    }
    public enum CampaignSaveStatus { Applied, AlreadyApplied, Rejected, OperationConflict }
    public sealed class CampaignSaveResult
    {
        public CampaignSaveStatus status;
        public CampaignSaveEnvelope snapshot;
    }

    // The caller supplies stable operation IDs and action keys including relevant parameters.
    // A rejected operation or an IO error never acknowledges a state transition.
    public sealed class CampaignSaveStore
    {
        private readonly string path, lockPath;
        public CampaignSaveStore(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath) || !Path.IsPathRooted(absolutePath)) throw new ArgumentException("An absolute save path is required.");
            path = Path.GetFullPath(absolutePath); lockPath = path + ".lock";
        }
        public static CampaignSaveStore ForSlot(string slotId)
        {
            if (string.IsNullOrWhiteSpace(slotId) || slotId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || slotId == "." || slotId == "..") throw new ArgumentException("A valid slot ID is required.");
            return new CampaignSaveStore(Path.Combine(Application.persistentDataPath, "FarSignal", "campaign-" + slotId + ".json"));
        }
        private FileStream Lock()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        public void Create(CampaignState initial)
        {
            if (initial == null) throw new ArgumentNullException(nameof(initial));
            using (Lock())
            {
                if (File.Exists(path)) throw new InvalidOperationException("The campaign save already exists.");
                Write(new CampaignSaveEnvelope { state = initial });
            }
        }
        public CampaignSaveEnvelope Load()
        {
            using (Lock()) return Read();
        }
        public CampaignSaveResult Apply(string operationId, string actionKey, Func<CampaignState, bool> operation)
        {
            if (string.IsNullOrWhiteSpace(operationId) || string.IsNullOrWhiteSpace(actionKey) || operation == null) throw new ArgumentException("Operation ID, action key and operation are required.");
            using (Lock())
            {
                var save = Read();
                var previous = save.operations.Find(x => x.id == operationId);
                if (previous != null) return new CampaignSaveResult { status = previous.actionKey == actionKey ? CampaignSaveStatus.AlreadyApplied : CampaignSaveStatus.OperationConflict, snapshot = save };
                if (!operation(save.state)) return new CampaignSaveResult { status = CampaignSaveStatus.Rejected, snapshot = Read() };
                save.operations.Add(new AppliedCampaignOperation { id = operationId, actionKey = actionKey });
                checked { save.revision++; }
                Write(save);
                return new CampaignSaveResult { status = CampaignSaveStatus.Applied, snapshot = save };
            }
        }
        private CampaignSaveEnvelope Read()
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Campaign save not found.", path);
            CampaignSaveEnvelope save;
            try { save = JsonUtility.FromJson<CampaignSaveEnvelope>(File.ReadAllText(path, Encoding.UTF8)); }
            catch (Exception e) { throw new InvalidDataException("Campaign save could not be read.", e); }
            if (save == null || save.version != 1 || save.revision < 0 || save.state == null || save.operations == null || save.operations.Exists(x => x == null || string.IsNullOrWhiteSpace(x.id) || string.IsNullOrWhiteSpace(x.actionKey)))
                throw new InvalidDataException("Campaign save is invalid or uses an unsupported version.");
            var seen = new HashSet<string>();
            foreach (var operation in save.operations) if (!seen.Add(operation.id)) throw new InvalidDataException("Campaign save contains duplicate operation IDs.");
            return save;
        }
        private void Write(CampaignSaveEnvelope save)
        {
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(save));
                using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { file.Write(bytes, 0, bytes.Length); file.Flush(true); }
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
