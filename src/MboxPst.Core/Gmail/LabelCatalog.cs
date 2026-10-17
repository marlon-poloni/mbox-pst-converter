namespace MboxPst.Core.Gmail;

public static class LabelCatalog
{
    private static readonly (string Prefix, HashSet<string> Names)[] Categories =
    {
        ("Categoria:", new(StringComparer.OrdinalIgnoreCase)
            { "pessoal", "social", "redes sociais", "promoções", "atualizações", "fóruns", "compras", "viagens", "finanças" }),
        ("Category ", new(StringComparer.OrdinalIgnoreCase)
            { "Personal", "Social", "Promotions", "Updates", "Forums", "Purchases", "Travel", "Finance" }),
    };

    private static readonly Dictionary<string, LabelKind> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Caixa de entrada"] = LabelKind.Inbox,
        ["Inbox"] = LabelKind.Inbox,
        ["Enviado"] = LabelKind.Sent,
        ["Enviados"] = LabelKind.Sent,
        ["Sent"] = LabelKind.Sent,
        ["Rascunhos"] = LabelKind.Drafts,
        ["Drafts"] = LabelKind.Drafts,
        ["Arquivada"] = LabelKind.Archived,
        ["Archived"] = LabelKind.Archived,
        ["Spam"] = LabelKind.Spam,
        ["Lixeira"] = LabelKind.Trash,
        ["Trash"] = LabelKind.Trash,
        ["Não lidas"] = LabelKind.Unread,
        ["Unread"] = LabelKind.Unread,
        ["Com estrela"] = LabelKind.Starred,
        ["Starred"] = LabelKind.Starred,
        ["IMAP_$Forwarded"] = LabelKind.Forwarded,
        ["$Forwarded"] = LabelKind.Forwarded,
        ["Aberto"] = LabelKind.Opened,
        ["Opened"] = LabelKind.Opened,
        ["Importante"] = LabelKind.Important,
        ["Important"] = LabelKind.Important,
    };

    public static LabelKind Classify(string label)
    {
        string name = label.Trim();
        if (Names.TryGetValue(name, out LabelKind kind))
            return kind;
        foreach (var (prefix, names) in Categories)
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && names.Contains(name[prefix.Length..].Trim()))
                return LabelKind.Category;
        return LabelKind.User;
    }
}
