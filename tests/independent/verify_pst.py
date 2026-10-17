"""Confere um PST com o libpff, um leitor que não tem relação com o gravador.

Uso: verify_pst.py [--amostra] <esperado.json> <arquivo.pst> [<arquivo-2.pst> ...]
--amostra: o esperado é só uma amostra; e-mails a mais no PST não contam como erro.
esperado.json: [{"messageId", "folders": ["A", "A/B"], "subject", "senderEmail", "isRead",
                 "isFlagged", "isForwarded", "attachments": [{"name", "sha256"}]}]
Sai com 0 se tudo bate; 1 com a lista de diferenças.
"""
import hashlib
import json
import re
import sys

import pypff

MSGFLAG_READ = 0x0001
PID_MESSAGE_FLAGS = 0x0E07
PID_FLAG_STATUS = 0x1090
PID_INTERNET_MESSAGE_ID = 0x1035
PID_SENDER_EMAIL = 0x0C1F
PID_LAST_VERB = 0x1081
PID_ATTACH_LONG_FILENAME = 0x3707


def entry_int(message, entry_type):
    for record_set in range(message.number_of_record_sets):
        records = message.get_record_set(record_set)
        for i in range(records.number_of_entries):
            entry = records.get_entry(i)
            if entry.entry_type == entry_type and entry.data:
                return int.from_bytes(entry.data[:4], "little")
    return None


def entry_text(message, entry_type):
    for record_set in range(message.number_of_record_sets):
        records = message.get_record_set(record_set)
        for i in range(records.number_of_entries):
            entry = records.get_entry(i)
            if entry.entry_type == entry_type and entry.data:
                return entry.data.decode("utf-16-le").rstrip("\x00")
    return None


def walk(folder, path, found):
    for i in range(folder.number_of_sub_messages):
        message = folder.get_sub_message(i)
        message_id = (entry_text(message, PID_INTERNET_MESSAGE_ID) or "").strip("<>")
        flags = entry_int(message, PID_MESSAGE_FLAGS) or 0
        attachments = []
        for a in range(message.number_of_attachments):
            attachment = message.get_attachment(a)
            data = attachment.read_buffer(attachment.size) if attachment.size else b""
            attachments.append({
                "name": entry_text(attachment, PID_ATTACH_LONG_FILENAME) or "",
                "sha256": hashlib.sha256(data).hexdigest().upper(),
            })
        found.setdefault(message_id, []).append({
            "folder": path,
            "subject": message.subject,
            "senderEmail": entry_text(message, PID_SENDER_EMAIL),
            "isRead": bool(flags & MSGFLAG_READ),
            "isFlagged": entry_int(message, PID_FLAG_STATUS) == 2,
            "isForwarded": entry_int(message, PID_LAST_VERB) == 104,
            "attachments": attachments,
        })
    for i in range(folder.number_of_sub_folders):
        child = folder.get_sub_folder(i)
        walk(child, f"{path}/{child.name}" if path else child.name, found)


def top_of_store(pst):
    root = pst.get_root_folder()
    for i in range(root.number_of_sub_folders):
        child = root.get_sub_folder(i)
        if child.name and re.search(r"top of", child.name, re.I):
            return child
    return root


def main(expected_path, pst_paths, sample=False):
    found = {}
    for pst_path in pst_paths:
        pst = pypff.file()
        pst.open(pst_path)
        top = top_of_store(pst)
        for i in range(top.number_of_sub_folders):
            child = top.get_sub_folder(i)
            walk(child, child.name, found)

    problems = []
    expected_all = json.load(open(expected_path, encoding="utf-8"))
    if not sample:
        for extra in sorted(set(found) - {e["messageId"] for e in expected_all}):
            problems.append(f"{extra}: e-mail no PST que não estava no esperado")
    for expected in expected_all:
        copies = found.get(expected["messageId"], [])
        folders = sorted(c["folder"] for c in copies)
        if folders != sorted(expected["folders"]):
            problems.append(f'{expected["messageId"]}: pastas {folders}, esperado {sorted(expected["folders"])}')
        for c in copies:
            for key in ("subject", "senderEmail", "isRead", "isFlagged", "isForwarded", "attachments"):
                if key in expected and c[key] != expected[key]:
                    problems.append(f'{expected["messageId"]} em {c["folder"]}: {key}={c[key]!r}, esperado {expected[key]!r}')

    if problems:
        print("\n".join(problems))
        return 1
    print(f"ok: {len(found)} e-mails conferidos")
    return 0


if __name__ == "__main__":
    arguments = sys.argv[1:]
    sample = "--amostra" in arguments
    arguments = [a for a in arguments if a != "--amostra"]
    sys.exit(main(arguments[0], arguments[1:], sample))
