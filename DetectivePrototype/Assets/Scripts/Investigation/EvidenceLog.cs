using System.Collections.Generic;

namespace Detective.Investigation
{
    /// <summary>획득한 단서(런타임 상태, 순수 C#). 같은 단서를 두 번 조사해도 한 번만 등록된다.</summary>
    public sealed class EvidenceLog
    {
        private readonly HashSet<string> _ids = new HashSet<string>();
        private readonly List<string> _order = new List<string>();

        /// <summary>새로 얻었으면 true, 이미 있거나 id가 비어 있으면 false.</summary>
        public bool Collect(string evidenceId)
        {
            if (string.IsNullOrEmpty(evidenceId)) return false;
            if (!_ids.Add(evidenceId)) return false;
            _order.Add(evidenceId);
            return true;
        }

        public bool Has(string evidenceId)
        {
            return !string.IsNullOrEmpty(evidenceId) && _ids.Contains(evidenceId);
        }

        public int Count { get { return _order.Count; } }

        /// <summary>획득한 순서.</summary>
        public IList<string> InOrder { get { return _order.AsReadOnly(); } }
    }
}
