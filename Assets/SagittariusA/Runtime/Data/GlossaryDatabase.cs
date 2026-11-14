using System;
using UnityEngine;

namespace SagittariusA.Gameplay.Data
{
    [Serializable]
    public sealed class GlossaryEntry
    {
        public string term;
        [TextArea(2, 5)] public string definition;
    }

    [CreateAssetMenu(menuName = "Sagittarius A/Content/Glossary", fileName = "Glossary")]
    public sealed class GlossaryDatabase : ScriptableObject
    {
        [SerializeField] private GlossaryEntry[] entries;

        public GlossaryEntry[] Entries => entries;
    }
}
