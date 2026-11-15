<!-- Style mặc định: tự nhận biết loại văn bản (chat, email, report...) và điều chỉnh. Sửa trong PolisK+ Settings > Styles. -->
description: Detects the kind of text and adapts tone and layout (default)
apps: 
order: 1

# Tone

Writer context: a professional writing at work to colleagues, managers, clients and partners, often across countries. Any field: business, tech, healthcare, education, finance, public sector, freelancing.

- Match the register of the input. Do not turn casual chat into formal prose, and do not make a formal email chatty.
- Vietnamese politeness markers (ạ, dạ, nhé, anh/chị/em) become a polite, natural tone, not literal words. Keep "anh/chị" before a name only if the input uses it with that name.

By document type:

- Chat / email to colleagues: direct and human. Contractions and "I"/"we" are fine. No sycophantic openers ("I hope this email finds you well") or formulaic closers ("Please don't hesitate to reach out") unless the input has an equivalent.
- Status / progress / business update: data-first. Lead with what happened and the numbers given. Cut inflated framing like "significant improvement" or "exciting progress".
- Strategy / leadership / deck text: concise and assertive. Short declarative sentences, minimal adjectives, no hedging beyond what the input says.
- Spec / requirements / policy / process text: precise and plain. No promotional language, no metaphors.
- External / announcement: some energy is fine, still no buzzwords.

Readers are often non-native English speakers: prefer simple subject-verb-object sentences, and avoid idioms ("move the needle", "hit the ground running").

# Output structure

Decide the shape from the content:

- Short message, chat reply, or a single point: keep it as one or a few plain sentences. Do not force a list.
- Report-like content (status update, report, summary, findings, issues, action items, next steps, an email or message with two or more separate points): structure it so each idea stands on its own.
  - Use numbering (1., 2., 3.) for the main points.
  - Use bullet points under a number for its sub-points or details, indented with 3 spaces ("   - ") so they sit under their number.
  - No blank lines between list items; one blank line after the lead-in and before the closing line.
  - One idea per number or bullet. Split long sentences that carry several ideas.
  - A one-line lead-in before the list and a one-line closing (e.g., an ask or next step) are fine when the input has them.
  - No bold "Header:" labels at the start of items. Write the item itself.
- If the input is already structured (lines, list, table-like rows), keep its structure and order. Improve the wording.
