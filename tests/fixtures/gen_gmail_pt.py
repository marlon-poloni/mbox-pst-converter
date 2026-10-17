"""Gera tests/fixtures/gmail-pt.mbox: 12 e-mails sintéticos no formato do Google Takeout."""
import base64
import random
from pathlib import Path

OUT = Path(__file__).with_name("gmail-pt.mbox")

MESSAGES = [
    ("Caixa de entrada,Aberto,Categoria: pessoal", "Bom dia", None),
    ("Caixa de entrada,Não lidas,Importante", "Reunião amanhã", None),
    ("Enviado,IMAP_$Forwarded", "Fwd: Proposta", None),
    ("Caixa de entrada,Com estrela", "Contrato assinado", None),
    ("Spam,Não lidas", "Você ganhou", None),
    ("Lixeira,Aberto", "Newsletter antiga", None),
    ("Fornecedores,Caixa de entrada", "Pedido 1234", None),
    ("Clientes/ACME", "Orçamento ACME", None),
    ("Caixa de entrada,Fornecedores,Enviado", "Re: Pedido 1234", None),
    ("Importante,Categoria: promoções", "Promoção de verão", None),
    ("=?UTF-8?Q?Caixa_de_entrada,Categoria:_atualiza=C3=A7=C3=B5es?=\n =?UTF-8?Q?,N=C3=A3o_lidas?=",
     "=?iso-8859-1?Q?Contribuinte_=96_ACME_LTDA?=", None),
    ("Rascunhos", "Relatório com anexo", "anexo"),
]


def message(i: int, labels: str, subject: str, attachment: str | None) -> str:
    head = (
        f"From 18000000000000{i:02d}@xxx Mon Oct 13 10:{i:02d}:00 +0000 2026\n"
        f"X-GM-THRID: 18000000000000{i:02d}\n"
        f"X-Gmail-Labels: {labels}\n"
        f"Message-ID: <msg{i:02d}@exemplo.com.br>\n"
        f"Date: Mon, 13 Oct 2026 10:{i:02d}:00 -0300\n"
        f"From: Remetente {i} <remetente{i}@exemplo.com.br>\n"
        f"To: Destino <destino@exemplo.com.br>\n"
        + ("Cc: Copia <copia@exemplo.com.br>\n" if i % 3 == 0 else "")
        + f"Subject: {subject}\n"
        "MIME-Version: 1.0\n"
    )
    html = f"<html><body><p>Mensagem número {i} com acentuação: ação, coração.</p></body></html>"
    if attachment is None:
        return head + (
            'Content-Type: multipart/alternative; boundary="alt"\n\n'
            "--alt\nContent-Type: text/plain; charset=UTF-8\nContent-Transfer-Encoding: 8bit\n\n"
            f"Mensagem número {i} com acentuação: ação, coração.\n"
            "--alt\nContent-Type: text/html; charset=UTF-8\nContent-Transfer-Encoding: 8bit\n\n"
            f"{html}\n--alt--\n"
        )
    rnd = random.Random(42)
    big = bytes(rnd.getrandbits(8) for _ in range(600 * 1024))
    small = "nome;valor\nação;1\n".encode("utf-8")
    b64 = lambda data: "\n".join(base64.b64encode(data).decode()[k:k + 76] for k in range(0, len(base64.b64encode(data)), 76))
    return head + (
        'Content-Type: multipart/mixed; boundary="mix"\n\n'
        "--mix\nContent-Type: text/html; charset=UTF-8\nContent-Transfer-Encoding: 8bit\n\n"
        f"{html}\n"
        '--mix\nContent-Type: application/pdf; name="relatorio.pdf"\n'
        'Content-Disposition: attachment; filename="relatorio.pdf"\nContent-Transfer-Encoding: base64\n\n'
        f"{b64(big)}\n"
        '--mix\nContent-Type: text/csv; charset=UTF-8; name="planilha.csv"\n'
        'Content-Disposition: attachment; filename="planilha.csv"\nContent-Transfer-Encoding: base64\n\n'
        f"{b64(small)}\n--mix--\n"
    )


def main() -> None:
    parts = [message(i + 1, *m) for i, m in enumerate(MESSAGES)]
    OUT.write_bytes("\n".join(parts).encode("utf-8"))


if __name__ == "__main__":
    main()
