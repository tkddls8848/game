using System.Collections.Generic;
using Detective.Data;

namespace Detective.Dialogue
{
    /// <summary>인물별 대사 파일 조회(순수 C#).</summary>
    public sealed class DialogueCatalog
    {
        private readonly List<DialogueFile> _files = new List<DialogueFile>();
        private readonly Dictionary<string, DialogueFile> _byNpc = new Dictionary<string, DialogueFile>();

        public DialogueCatalog(IEnumerable<DialogueFile> files)
        {
            if (files == null) return;
            foreach (DialogueFile file in files)
            {
                if (file == null) continue;
                file.Normalized();
                _files.Add(file);
                if (!string.IsNullOrEmpty(file.npcId)) _byNpc[file.npcId] = file;
            }
        }

        public IList<DialogueFile> Files { get { return _files.AsReadOnly(); } }

        /// <summary>그 인물의 대사. 파일이 없으면 빈 배열.</summary>
        public DialogueLine[] LinesOf(string npcId)
        {
            DialogueFile file;
            if (!string.IsNullOrEmpty(npcId) && _byNpc.TryGetValue(npcId, out file)) return file.lines;
            return new DialogueLine[0];
        }
    }
}
