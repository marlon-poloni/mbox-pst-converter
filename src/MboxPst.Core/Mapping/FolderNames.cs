using MboxPst.Core.Gmail;

namespace MboxPst.Core.Mapping;

public static class FolderNames
{
    public const string Inbox = "Caixa de Entrada";
    public const string Sent = "Itens Enviados";
    public const string Drafts = "Rascunhos";
    public const string Archive = "Arquivo Morto";
    public const string Junk = "Lixo Eletrônico";
    public const string Deleted = "Itens Excluídos";

    public static readonly IReadOnlyList<string> StandardOrder = new[] { Inbox, Sent, Drafts, Archive, Junk, Deleted };

    public const int MaxLength = 255;

    public static string? Validate(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Dê um nome para a pasta.";
        if (name.Length > MaxLength)
            return $"O nome da pasta pode ter no máximo {MaxLength} caracteres.";
        return null;
    }

    public static string For(LabelKind kind) => kind switch
    {
        LabelKind.Inbox => Inbox,
        LabelKind.Sent => Sent,
        LabelKind.Drafts => Drafts,
        LabelKind.Archived => Archive,
        LabelKind.Spam => Junk,
        LabelKind.Trash => Deleted,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Tipo de marcador sem pasta padrão."),
    };
}
