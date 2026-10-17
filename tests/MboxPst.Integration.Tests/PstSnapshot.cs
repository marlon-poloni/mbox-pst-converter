using System.Security.Cryptography;
using PSTFileFormat;

namespace MboxPst.Integration.Tests;

public sealed record PstAttachment(string Name, string Sha256);

public sealed record PstItem(
    string Folder,
    string? MessageId,
    string? Subject,
    string? SenderEmail,
    string? DisplayTo,
    DateTime? Date,
    bool IsRead,
    bool IsFlagged,
    bool IsForwarded,
    IReadOnlyList<PstAttachment> Attachments);

public static class PstSnapshot
{
    private const int MsgFlagRead = 0x0001;

    public static string StoreName(string pstPath)
    {
        var pst = new PSTFile(pstPath, FileAccess.Read);
        try
        {
            return PSTNode.GetPSTNode(pst, new NodeID(0x21)).PC.GetStringProperty(PropertyID.PidTagDisplayName);
        }
        finally { pst.CloseFile(); }
    }

    public static IReadOnlyList<PstItem> Read(IEnumerable<string> pstPaths)
    {
        var items = new List<PstItem>();
        foreach (string path in pstPaths)
        {
            var pst = new PSTFile(path, FileAccess.Read);
            try
            {
                foreach (PSTFolder child in pst.TopOfPersonalFolders.GetChildFolders())
                    Walk(child, child.DisplayName, items);
            }
            finally { pst.CloseFile(); }
        }
        return items;
    }

    private static string? Subject(string? raw) =>
        raw is { Length: >= 2 } && raw[0] == '\u0001' ? raw[2..] : raw;

    private static void Walk(PSTFolder folder, string path, List<PstItem> items)
    {
        for (int i = 0; i < folder.MessageCount; i++)
        {
            MessageObject message = folder.GetMessage(i);
            var attachments = new List<PstAttachment>();
            for (int a = 0; a < message.AttachmentCount; a++)
            {
                AttachmentObject attachment = message.GetAttachmentObject(a);
                byte[] data = attachment.PC.GetBytesProperty(PropertyID.PidTagAttachData) ?? Array.Empty<byte>();
                attachments.Add(new PstAttachment(
                    attachment.PC.GetStringProperty(PropertyID.PidTagAttachLongFilename) ?? "",
                    Convert.ToHexString(SHA256.HashData(data))));
            }
            int flags = message.PC.GetInt32Property(PropertyID.PidTagMessageFlags) ?? 0;
            items.Add(new PstItem(
                path,
                message.PC.GetStringProperty(PropertyID.PidTagInternetMessageId),
                Subject(message.PC.GetStringProperty(PropertyID.PidTagSubject)),
                message.PC.GetStringProperty(PropertyID.PidTagSenderEmailAddress),
                message.PC.GetStringProperty(PropertyID.PidTagDisplayTo),
                message.PC.GetDateTimeProperty(PropertyID.PidTagClientSubmitTime),
                (flags & MsgFlagRead) != 0,
                message.PC.GetInt32Property(PropertyID.PidTagFlagStatus) == 2,
                message.PC.GetInt32Property(PropertyID.PidTagLastVerbExecuted) == 104,
                attachments));
        }
        foreach (PSTFolder child in folder.GetChildFolders())
            Walk(child, path + "/" + child.DisplayName, items);
    }
}
