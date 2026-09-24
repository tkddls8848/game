using System.Collections.Generic;
using Detective.Data;

namespace Detective.Investigation
{
    /// <summary>evidence.json 조회(순수 C#). 파일에 적힌 순서를 그대로 유지한다.</summary>
    public sealed class EvidenceCatalog
    {
        private readonly List<EvidenceDefinition> _all = new List<EvidenceDefinition>();
        private readonly List<PropDefinition> _props = new List<PropDefinition>();
        private readonly Dictionary<string, EvidenceDefinition> _byId = new Dictionary<string, EvidenceDefinition>();

        public EvidenceCatalog(EvidenceTable table)
        {
            if (table == null) return;
            table.Normalized();

            for (int i = 0; i < table.evidence.Length; i++)
            {
                EvidenceDefinition evidence = table.evidence[i];
                if (evidence == null) continue;
                _all.Add(evidence);
                if (!string.IsNullOrEmpty(evidence.id)) _byId[evidence.id] = evidence;
            }
            for (int i = 0; i < table.props.Length; i++)
            {
                if (table.props[i] != null) _props.Add(table.props[i]);
            }
        }

        public IList<EvidenceDefinition> All { get { return _all.AsReadOnly(); } }
        public IList<PropDefinition> Props { get { return _props.AsReadOnly(); } }

        public bool TryGet(string id, out EvidenceDefinition evidence)
        {
            evidence = null;
            if (string.IsNullOrEmpty(id)) return false;
            return _byId.TryGetValue(id, out evidence);
        }

        public string NameOf(string id)
        {
            EvidenceDefinition evidence;
            return TryGet(id, out evidence) ? evidence.name : id;
        }
    }
}
