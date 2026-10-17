#nullable enable
using System.Security.Cryptography;
using Mail2Pst.Core.Mapping;
using Mail2Pst.Core.Models;
using Mail2Pst.Core.Reporting;
using Mail2Pst.Core.Writing;
using PSTFileFormat;
using Xunit;

namespace Mail2Pst.Core.Tests.Writing;

public sealed class MultiFolderWriteTests : IDisposable
{
    private const uint NidMessageStore = 0x21;
    private const uint NidDeletedItems = 0x8062;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mboxpst-multi-" + Guid.NewGuid());

    public MultiFolderWriteTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private static MailMessage Message(params MailAttachment[] attachments) => new()
    {
        Subject = "Pedido 1234",
        From = new MailAddress { Name = "Ana", Email = "ana@exemplo.com.br" },
        To = new List<MailAddress> { new() { Email = "bruno@exemplo.com.br" } },
        TextBody = "corpo",
        Source = new SourceReference { SourcePath = "x.mbox", Identifier = "msg#1" },
        Attachments = attachments.ToList(),
    };

    private string Write(PstWriter writer, MailMessage message, params string[][] paths)
    {
        var plan = new PstOutputPlan { Name = "Teste", MaxSizeBytes = 1L << 30 };
        var planned = new[] { new PlannedMessage { Message = message, TargetFolderPaths = paths } };
        return writer.WritePlan(plan, planned, _dir, new ConversionReport()).Single();
    }

    private static byte[] AttachmentBytes(PSTFolder folder, int note = 0) =>
        ((MailFolder)folder).GetNote(note).GetAttachmentObject(0).PC.GetBytesProperty(PropertyID.PidTagAttachData)!;

    [Fact]
    public void Mesmo_email_em_tres_pastas_com_anexo_pequeno()
    {
        byte[] data = RandomNumberGenerator.GetBytes(10_000);
        var attachment = new MailAttachment { FileName = "a.bin", MimeType = "application/octet-stream", Content = AttachmentContent.FromBytes(data) };

        string pstPath = Write(new PstWriter(), Message(attachment), new[] { "Caixa de Entrada" }, new[] { "Fornecedores" }, new[] { "Clientes", "ACME" });

        var pst = new PSTFile(pstPath, FileAccess.Read);
        try
        {
            PSTFolder top = pst.TopOfPersonalFolders;
            PSTFolder[] folders =
            {
                top.FindChildFolder("Caixa de Entrada")!,
                top.FindChildFolder("Fornecedores")!,
                top.FindChildFolder("Clientes")!.FindChildFolder("ACME")!,
            };
            foreach (PSTFolder folder in folders)
            {
                Note note = ((MailFolder)folder).GetNote(0);
                Assert.Equal("Pedido 1234", note.Subject);
                Assert.Equal(data, AttachmentBytes(folder));
            }
        }
        finally { pst.CloseFile(); }
    }

    [Fact]
    public void Mesmo_email_em_duas_pastas_com_anexo_em_arquivo_temporario()
    {
        byte[] data = RandomNumberGenerator.GetBytes(20 * 1024 * 1024);
        string temp = Path.Combine(_dir, "anexo.tmp");
        File.WriteAllBytes(temp, data);
        var attachment = new MailAttachment { FileName = "grande.bin", MimeType = "application/octet-stream", Content = AttachmentContent.FromTempFile(temp, data.Length) };

        string pstPath = Write(new PstWriter(), Message(attachment), new[] { "Caixa de Entrada" }, new[] { "Fornecedores" });

        Assert.False(File.Exists(temp));
        var pst = new PSTFile(pstPath, FileAccess.Read);
        try
        {
            PSTFolder top = pst.TopOfPersonalFolders;
            Assert.Equal(SHA256.HashData(data), SHA256.HashData(AttachmentBytes(top.FindChildFolder("Caixa de Entrada")!)));
            Assert.Equal(SHA256.HashData(data), SHA256.HashData(AttachmentBytes(top.FindChildFolder("Fornecedores")!)));
        }
        finally { pst.CloseFile(); }
    }

    [Fact]
    public void Caminho_unico_continua_funcionando()
    {
        var planned = new PlannedMessage { Message = Message(), TargetFolderPath = new[] { "Inbox" } };
        Assert.Equal(new[] { new[] { "Inbox" } }, planned.TargetFolderPaths);
    }

    [Fact]
    public void Nome_do_arquivo_de_dados_e_o_escolhido()
    {
        string pstPath = Write(new PstWriter { StoreDisplayName = "Todos os e-mails" }, Message(), new[] { "Caixa de Entrada" });

        var pst = new PSTFile(pstPath, FileAccess.Read);
        try
        {
            PSTNode store = PSTNode.GetPSTNode(pst, new NodeID(NidMessageStore));
            Assert.Equal("Todos os e-mails", store.PC.GetStringProperty(PropertyID.PidTagDisplayName));
        }
        finally { pst.CloseFile(); }
    }

    [Fact]
    public void Lixeira_vai_para_a_pasta_especial_de_excluidos()
    {
        var writer = new PstWriter { DeletedItemsDisplayName = "Itens Excluídos" };
        string pstPath = Write(writer, Message(), new[] { "Itens Excluídos" });

        var pst = new PSTFile(pstPath, FileAccess.Read);
        try
        {
            PSTFolder[] matches = pst.TopOfPersonalFolders.GetChildFolders().Where(f => f.DisplayName == "Itens Excluídos").ToArray();
            PSTFolder deleted = Assert.Single(matches);
            Assert.Equal(NidDeletedItems, deleted.NodeID.Value);
            Assert.Equal(1, deleted.MessageCount);
            Assert.DoesNotContain(pst.TopOfPersonalFolders.GetChildFolders(), f => f.DisplayName == "Deleted Items");
        }
        finally { pst.CloseFile(); }
    }
}
