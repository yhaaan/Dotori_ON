using System;
using System.IO;
using UnityEngine;

namespace DOTORION.Events.Subjugation
{
    public sealed class SubjugationStore
    {
        private readonly string _path;
        public SubjugationStore(string directory, string memberId)
        {
            // Backend ids are GUIDs in production; escaping also keeps mock names inside this directory.
            _path = Path.Combine(directory, "chiikawa-v1-" + Uri.EscapeDataString(memberId) + ".json");
        }

        public SubjugationGame Load(SubjugationBalance balance)
        {
            Exception failure = null;
            foreach (var candidate in new[] { _path, _path + ".bak" })
            {
                if (!File.Exists(candidate)) continue;
                try
                {
                    var data = JsonUtility.FromJson<SubjugationSave>(File.ReadAllText(candidate));
                    if (data == null) throw new ArgumentException("Empty event save");
                    return new SubjugationGame(data, balance);
                }
                catch (Exception e) when (e is IOException || e is ArgumentException || e is UnauthorizedAccessException) { failure = e; }
            }
            if (failure != null) throw new IOException("이벤트 저장을 읽지 못했습니다. 기존 파일은 보존됩니다.", failure);
            return new SubjugationGame(null, balance);
        }

        /// <summary>Removes this account's save and its backups, so nothing can be recovered from them.</summary>
        public void Delete()
        {
            foreach (var path in new[] { _path, _path + ".bak", _path + ".tmp" })
                if (File.Exists(path)) File.Delete(path);
        }

        public void Write(SubjugationSave save)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            var temp = _path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(save, true));
            if (File.Exists(_path)) File.Replace(temp, _path, _path + ".bak");
            else File.Move(temp, _path);
        }
    }
}
