# Validação no Outlook

O PST gerado já é conferido de duas formas automáticas: o próprio conversor relê o arquivo
pasta por pasta, e os testes usam um leitor independente (libpff). Falta conferir no Outlook
de verdade, que é quem vai abrir o arquivo. Isso precisa de um Windows com o Outlook clássico
(o Outlook na web e o "novo Outlook" não abrem PST).

## Onde rodar

- Uma máquina virtual com Windows e Outlook clássico, ou um computador Windows emprestado.
- Alternativa sem Windows: o serviço de importação de PST do Microsoft 365 (o envio é feito
  com o AzCopy, que roda no Linux) e conferência no Outlook na web.

## O que conferir (arquivo de 6,2 GB do teste, de-para padrão)

1. **Abrir:** Arquivo > Abrir e Exportar > Abrir Arquivo de Dados do Outlook. O arquivo abre sem
   pedir reparo e aparece com o nome "Todos os e-mails".
2. **Pastas e quantidades:**

   | Pasta | E-mails |
   |---|---|
   | Caixa de Entrada | 5.773 |
   | Itens Enviados | 703 |
   | Rascunhos | 24 |
   | Arquivo Morto | 163 |
   | Lixo Eletrônico | 76 |
   | Itens Excluídos | 7 |
   | Faturas, Fornecedores, Processos 2025, Notes, Marketing, Compras (nomes reais dos seus marcadores) | 124, 46, 17, 12, 6, 3 |

3. **Itens Excluídos aparece uma vez só** (sem uma "Deleted Items" ao lado).
4. **Não lidos:** 1.068 e-mails em negrito (somando as pastas).
5. **Sinalizados:** 6 e-mails com a bandeira vermelha.
6. **Anexos:** abrir alguns PDFs e planilhas; abrir um e-mail anexado (`.eml`).
7. **Acentos:** assuntos com ç, ã, é e o travessão "–" aparecem certos.
8. **Importar numa caixa existente:** Arquivo > Abrir e Exportar > Importar/Exportar > Importar de
   outro programa ou arquivo > Arquivo de Dados do Outlook (.pst). Conferir se as pastas padrão
   caem nas pastas da caixa (e não em pastas duplicadas). Repetir com o Outlook em português e,
   se possível, em inglês.
9. **ScanPST (opcional):** rodar a Ferramenta de Reparo da Caixa de Entrada (`SCANPST.EXE`) no
   arquivo; ela não deve encontrar erros.

## Se algo não bater

Anote a pasta, o assunto de um e-mail de exemplo e o que apareceu. Cada diferença vira um teste
novo antes da correção.
