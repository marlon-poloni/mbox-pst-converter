using MboxPst.Core.Previewing;
using PSTFileFormat;

namespace MboxPst.Core.Conversion;

public static class OutputVerifier
{
    public static IReadOnlyList<FolderCheck> Check(IReadOnlyList<string> pstFiles, FolderNode expectedRoot)
    {
        var found = new Dictionary<string, (IReadOnlyList<string> Path, int Count)>(StringComparer.Ordinal);
        foreach (string file in pstFiles)
        {
            var pst = new PSTFile(file, FileAccess.Read);
            try
            {
                foreach (PSTFolder child in pst.TopOfPersonalFolders.GetChildFolders())
                    Count(child, new[] { child.DisplayName }, found);
            }
            finally { pst.CloseFile(); }
        }

        var checks = new List<FolderCheck>();
        var expectedKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (FolderNode node in Flatten(expectedRoot).Where(n => n.Path.Count > 0))
        {
            string key = Key(node.Path);
            expectedKeys.Add(key);
            checks.Add(new FolderCheck(node.Path, node.Messages, found.TryGetValue(key, out var f) ? f.Count : 0));
        }
        foreach (var (key, (path, count)) in found)
            if (count > 0 && !expectedKeys.Contains(key))
                checks.Add(new FolderCheck(path, 0, count));
        return checks;
    }

    private static void Count(PSTFolder folder, IReadOnlyList<string> path, Dictionary<string, (IReadOnlyList<string>, int)> found)
    {
        string key = Key(path);
        int previous = found.TryGetValue(key, out var existing) ? existing.Item2 : 0;
        found[key] = (path, previous + folder.MessageCount);
        foreach (PSTFolder child in folder.GetChildFolders())
            Count(child, path.Append(child.DisplayName).ToArray(), found);
    }

    private static IEnumerable<FolderNode> Flatten(FolderNode node) => new[] { node }.Concat(node.Children.SelectMany(Flatten));

    private static string Key(IReadOnlyList<string> path) => string.Join('\u001F', path);
}
