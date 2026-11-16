# PolisK+

English: [README.md](README.md)

Bôi đen một đoạn tiếng Việt (hoặc Việt-Anh lẫn lộn) trong bất kỳ app nào (Outlook, SeaTalk, Word, Excel, Chrome...), nhấn **Ctrl+Alt+P**, nhận lại bản tiếng Anh tự nhiên, chuyên nghiệp trong một popup nhỏ. Giọng văn và cách trình bày tự đổi theo tình huống: chat, email, báo cáo, gửi sếp, gửi đối tác... Cần ngôn ngữ khác (tiếng Việt, Nhật, Trung...)? Đổi ngay trong popup.

Dùng được cho mọi ngành nghề có viết tiếng Anh trong công việc: văn phòng, dự án, sales, chăm sóc khách hàng, nhân sự, tài chính, kỹ thuật, y tế, giáo dục, nghiên cứu, freelancer.

PolisK+ chạy với AI bạn đang có: **Claude** bằng gói Claude của chính bạn (không cần API key), hoặc **OpenAI GPT**, **Google Gemini**, hay dịch vụ khác bằng API key của bạn. Không cần quyền admin.

## Yêu cầu

1. Windows 10/11 với Windows PowerShell 5.1 (có sẵn trong Windows).
2. Một tài khoản AI, chọn một trong các loại sau:

| AI | Cần gì |
|---|---|
| **Claude** (mặc định) | Gói Claude trả phí (Pro, Max, Team, Enterprise) và Claude Code CLI. Không cần API key. Setup hỏi để cài CLI và đăng nhập giúp (cài theo user, không cần admin) |
| **OpenAI GPT** | API key của OpenAI, đã bật thanh toán. Gói ChatGPT Plus không bao gồm quyền dùng API |
| **Google Gemini** | API key lấy ở Google AI Studio |
| **Other** | Dịch vụ bất kỳ có API tương thích OpenAI: OpenRouter, Groq, DeepSeek, Mistral, hoặc model chạy ngay trên máy bằng Ollama / LM Studio |

## Cài đặt

1. Vào trang [Releases](https://github.com/theimpto123/polisk-plus/releases/latest), mục **Assets**, tải file `PolisK+-<version>.zip`. (Từ bản 1.3.2, tải bằng nút **Code > Download ZIP** hoặc "Source code (zip)" cũng cài được.)
2. Chuột phải file zip > **Extract All...** để giải nén **toàn bộ**, rồi mở thư mục vừa giải nén. Đừng chạy Setup.cmd ngay bên trong file zip.
   - Nếu Windows chặn: chuột phải file zip > Properties > tick **Unblock** > OK, rồi giải nén lại.
3. Double-click **Setup.cmd**. Setup sẽ:
   1. Kiểm tra máy có chạy được PolisK+ không.
   2. Cài vào `%LOCALAPPDATA%\Programs\Polish`.
   3. Hỏi bạn dùng AI nào (chỉ hỏi ở lần cài đầu; gõ số 1-4).
   4. Nếu chọn Claude: tìm Claude Code CLI, hỏi cài nếu chưa có, hỏi đăng nhập nếu chưa đăng nhập (mở trình duyệt).
   5. Tạo icon **PolisK+** trong Start Menu và (nếu bạn đồng ý) trên Desktop, hỏi có tự chạy khi mở máy không, rồi khởi động PolisK+.
4. Nếu chọn AI khác Claude: cửa sổ Settings tự mở ở tab **AI provider**. Dán API key, bấm **Load models** và chọn model, bấm **Test connection**, rồi **Save**.
5. Thấy icon chữ **P** bạc trên nền xanh navy ở khay hệ thống (góc phải dưới, có thể nằm trong mũi tên ^) là xong.

Nâng cấp: chạy Setup.cmd của bản mới.

- Style, luật và config bạn đã sửa **được giữ nguyên**.
- File bạn chưa từng sửa được cập nhật lên nội dung mặc định mới.
- Nâng cấp từ bản 1.0: nếu bạn đã sửa `tone.md` / `structure.md`, nội dung đó trở thành style **My style** và được đặt làm mặc định. File cũ được giữ ở `prompt\old-v1`.

## Cách dùng

1. Bôi đen đoạn văn cần sửa.
2. Nhấn **Ctrl+Alt+P**. Popup hiện "Polishing..." gần con trỏ chuột (Esc hoặc Cancel để huỷ).
3. Dòng **Style** ở đầu popup cho biết đang dùng style nào và vì sao, ví dụ `2. Chat (auto: seatalk)`. Muốn kiểu khác thì chọn trong danh sách, hoặc nhấn **Ctrl+1** đến **Ctrl+9**: PolisK+ viết lại ngay theo style đó.
4. Ô **Language** bên cạnh: mỗi lần nhấn hotkey luôn bắt đầu bằng **English**. Muốn ngôn ngữ khác thì chọn trong ô này hoặc nhấn **Alt+1**, **Alt+2**...: PolisK+ viết lại cùng đoạn đó bằng ngôn ngữ mới. Ô chỉ liệt kê các ngôn ngữ bạn đã thêm (xem [Ngôn ngữ đầu ra](#ngôn-ngữ-đầu-ra)).
5. Sửa thêm trong popup nếu muốn, rồi chọn:
   - **Copy** (hoặc **Ctrl+Enter**): copy kết quả và đóng popup.
   - **Replace**: dán đè lên đoạn bạn đã bôi đen.
   - **Close** (hoặc **Esc**): đóng.

Clipboard cũ của bạn được trả lại sau khi PolisK+ lấy chữ, và sau khi Replace.

## Mở PolisK+ Settings

Một trong các cách sau:

- Double-click icon **PolisK+** trên Desktop, hoặc Start Menu > **PolisK+** (nếu PolisK+ chưa chạy thì nó cũng được khởi động).
- Double-click icon **P** ở khay hệ thống.
- Chuột phải icon **P** > **Settings...**

Bấm **Save** (hoặc Ctrl+S) là có hiệu lực ngay, không cần khởi động lại.

## Chọn AI (tab AI provider)

Settings > tab **AI provider**. Đổi lúc nào cũng được, bấm **Save** là dùng ngay.

1. **AI provider**: chọn Claude, OpenAI GPT, Google Gemini hoặc Other.
2. **API key** (không có với Claude): bấm **Get a key** để mở trang tạo key của nhà cung cấp, tạo key rồi dán vào ô. Key được lưu dạng mã hoá, chỉ tài khoản Windows của bạn trên máy này đọc được. **Remove key** để xoá.
3. **Model**: bấm **Load models** để lấy danh sách model mà key của bạn dùng được (mới nhất ở trên, tối đa 15 dòng), rồi chọn. Tên model của các hãng đổi thường xuyên nên đừng đoán. Model nhỏ ("mini", "flash") nhanh, rẻ và đủ tốt để chuốt câu.
4. **Base URL** (chỉ với Other): địa chỉ kết thúc bằng `/v1`, ví dụ `https://openrouter.ai/api/v1`, hoặc `http://localhost:11434/v1` nếu dùng Ollama trên máy (không cần key).
5. Bấm **Test connection** để thử trước khi Save. Lỗi thường gặp (sai key, sai model, hết quota) được báo bằng chữ dễ hiểu.

Mỗi AI nhớ model và key riêng, nên bạn chuyển qua lại mà không phải nhập lại.

## Ngôn ngữ đầu ra

Mặc định PolisK+ viết bằng **English**. Sau khi cài có sẵn 2 ngôn ngữ: **English** và **Vietnamese** (Vietnamese dùng để chuốt lại câu tiếng Việt mà không dịch).

Thêm ngôn ngữ khác: Settings > tab **General** > **Output languages**:

1. Gõ tên ngôn ngữ vào ô bên phải (gõ vài chữ là có gợi ý), bấm **Add** hoặc Enter. Gõ tên nào cũng được, ví dụ `Japanese`, `Chinese (Traditional)`, `Portuguese (Brazil)`, `English (British)`.
2. Ngôn ngữ đứng đầu danh sách là **mặc định** cho mỗi lần nhấn hotkey. Muốn đổi thì chọn ngôn ngữ rồi bấm **Make default**. **Move up** đổi thứ tự (cũng là số Alt+1..8 trong popup), **Remove** xoá.
3. Bấm **Save**.

Tối đa 8 ngôn ngữ, để ô Language trong popup luôn ngắn, không phải cuộn. Nếu chỉ còn 1 ngôn ngữ, popup ẩn ô Language.

Mỗi style dùng được với mọi ngôn ngữ: style quyết định giọng văn và cách trình bày, ngôn ngữ quyết định viết bằng tiếng gì. Muốn thử, dùng ô **Language** trong khung **Try this style** (tab Styles).

## Style: tone + structure theo tình huống

Mỗi **style** là một bộ *Tone* (giọng văn) + *Output structure* (cách trình bày) cho một tình huống. Có 8 style sẵn:

| # | Style | Dùng khi | Tự chọn khi bạn đang ở |
|---|---|---|---|
| 1 | **Auto** | Tự nhận biết chat / email / báo cáo và điều chỉnh (mặc định) | app không khớp style nào |
| 2 | **Chat** | Tin nhắn ngắn: thân thiện, thẳng ý, ít chữ | SeaTalk, Teams, Slack, Zalo, Telegram, WhatsApp |
| 3 | **Email** | Email công việc: lịch sự, rõ ràng, ý chính trước | Outlook, Gmail / Outlook trên web |
| 4 | **Report** | Weekly report, cập nhật tiến độ, biên bản họp: đánh số từng ý | (chọn tay) |
| 5 | **Leadership** | Gửi sếp: kết luận trước, rất ngắn | (chọn tay) |
| 6 | **Document** | Spec, quy trình, chính sách, tài liệu chính thức: chính xác, trung tính, giữ cấu trúc | Word, Confluence, Google Docs |
| 7 | **External** | Gửi khách hàng, đối tác, nhà cung cấp: trang trọng | (chọn tay) |
| 8 | **Light touch** | Chỉ sửa ngữ pháp, giữ nguyên cách viết của bạn | (chọn tay) |

Cách PolisK+ chọn style:

1. Nếu bật **Pick the style automatically from the app you are in** (tab General), PolisK+ xem app đang dùng có nằm trong ô **Apps** của style nào không. Nhiều style khớp thì lấy style đứng trên.
2. Không khớp thì dùng **Default style** (khuyên dùng: Auto).
3. Bạn luôn đổi được ngay trong popup bằng Ctrl+1..9.

### Sửa hoặc tạo style

PolisK+ Settings > tab **Styles (tone + structure)**:

1. Chọn style bên trái, sửa **Tone** và **Output structure** bên phải. Viết tiếng Anh hay tiếng Việt đều được, mỗi ý một dòng, ví dụ:
   - `Write in British English.`
   - `Always formal: no contractions, start emails with "Dear ...".`
   - `Never use lists; always write paragraphs.`
   - `Drop Vietnamese honorifics before names: "anh Minh" -> "Minh".`
2. Ô **Try this style** ở dưới: dán một đoạn mẫu, bấm **Try** để xem kết quả với nội dung đang sửa (chưa cần Save).
3. **New** tạo style trống, **Duplicate** nhân bản style đang chọn để chỉnh tiếp. **Move up / Move down** đổi thứ tự, cũng là số Ctrl+1..9 trong popup.
4. Ô **Apps**: tên process, cách nhau bằng dấu phẩy (`outlook, seatalk, winword`), hoặc `title:Chữ` để khớp tiêu đề cửa sổ (`title:Gmail`, `title:Confluence`). Để trống nếu chỉ muốn chọn tay.
5. **Restore defaults** đưa 8 style sẵn về nguyên bản. Style bạn tự tạo được giữ, bản cũ lưu dạng `.bak`.

Mẹo: muốn biết tên process của một app, mở Task Manager > tab Details, bỏ đuôi `.exe`.

Style là file `.md` thường trong thư mục `profiles`. Muốn chia sẻ style cho đồng nghiệp: Settings > Advanced > **Open settings folder**, gửi file `.md` trong `profiles`, người nhận chép vào thư mục `profiles` của họ.

Những điều cần biết:

1. Output là chữ thuần (plain text) để dán vào Outlook hoặc SeaTalk, nên đừng yêu cầu in đậm (sẽ hiện nguyên dấu `**`).
2. Style được ưu tiên hơn phần mặc định trong core rules, trừ 4 luật cứng: chỉ trả về đoạn đã sửa, không trả lời thay câu hỏi, giữ nguyên nghĩa, không bịa thông tin.
3. Đoạn nằm trong `<!-- ... -->` là ghi chú. PolisK+ tự bỏ đi, không gửi cho AI.

## Gợi ý cài đặt

| Mục (tab) | Khuyên dùng | Vì sao |
|---|---|---|
| Hotkey (General) | `Ctrl+Alt+P` | Ctrl+Shift+... hay trùng phím tắt của trình duyệt |
| Output languages (General) | `English` đứng đầu, chỉ thêm ngôn ngữ bạn thật sự dùng | Danh sách ngắn thì chọn nhanh bằng Alt+1..8 |
| Model với Claude (AI provider) | `sonnet` | Cân bằng nhất: 3-7 giây, dịch tiếng Việt tốt. `haiku` không nhanh hơn bao nhiêu nhưng dịch sai nhiều hơn. `opus` chất lượng cao nhất nhưng chậm hơn và tốn quota hơn |
| Model với OpenAI / Gemini (AI provider) | Model nhỏ trong danh sách **Load models** ("mini", "flash") | Nhanh, rẻ, đủ tốt để chuốt câu |
| Effort, chỉ với Claude (AI provider) | `(default)` | `low` chỉ nhanh hơn chút với đoạn ngắn |
| Timeout (AI provider) | `60` | Tăng nếu hay polish đoạn rất dài |
| Default style (General) | `Auto` | Tự nhận biết loại văn bản |
| Pick the style automatically (General) | Bật | Outlook ra Email, SeaTalk ra Chat, Word ra Document |
| Start PolisK+ when I sign in (General) | Bật | Sau khi đăng nhập Windows có thể mất vài phút PolisK+ mới chạy, vì Windows khởi động app lần lượt |
| Copy wait (Advanced) | `1000` | Tăng lên `2000` nếu hay bị báo "Nothing selected" |
| Replace delay (Advanced) | `700` | Tăng lên nếu Replace dán ra chữ cũ |

Nút **Use recommended settings** (tab General) đặt lại tab General theo bảng trên và giữ nguyên AI bạn đang chọn.

Tốc độ: với Claude thường mất 3-7 giây mỗi lần, chủ yếu là thời gian khởi động CLI và đường mạng. Các AI dùng API thường nhanh hơn.

## Bảo mật

1. PolisK+ không lưu đoạn văn của bạn ở đâu cả. File log `%LOCALAPPDATA%\Polish\polish.log` chỉ ghi lỗi (mã lỗi, timeout), không ghi nội dung. PolisK+ chỉ đọc tên app và tiêu đề cửa sổ để chọn style, và cũng không ghi lại thông tin đó.
2. Với Claude, mỗi lần gọi chạy ở chế độ chỉ đọc chữ, trả chữ:
   - Không có tool, không có MCP, không đọc settings hay hook của bạn.
   - Không lưu vào lịch sử session của Claude Code.
   - Chạy từ một thư mục rỗng.
3. Nội dung chỉ được gửi tới AI bạn chọn, theo tài khoản và điều khoản của bạn với nhà cung cấp đó. Hãy đọc điều khoản dữ liệu của họ: các gói API miễn phí có thể dùng nội dung bạn gửi để cải thiện model.
4. API key được lưu dạng mã hoá (Windows DPAPI) trong `config.json`: chỉ tài khoản Windows của bạn trên máy này giải mã được, nên chép file sang máy khác thì key không dùng được. Key không bao giờ được ghi vào log, và chỉ được gửi tới đúng nhà cung cấp của nó qua HTTPS.
5. Hãy tuân theo quy định dữ liệu của công ty bạn về việc dịch vụ AI nào được nhận nội dung công việc.

## Gỡ cài đặt

Start Menu > PolisK+ > **Uninstall PolisK+** (hoặc chạy `Uninstall.cmd` trong thư mục giải nén). Style, core rules và config được sao lưu vào `Documents\PolisK-settings-backup-...` trước khi xoá.

## Xử lý sự cố

| Hiện tượng | Cách xử lý |
|---|---|
| "Nothing selected" | Bôi đen lại rồi nhấn hotkey. App chậm thì tăng **Copy wait** (Settings > Advanced) |
| "Not signed in" (Claude) | Settings > Advanced > **Sign in...**, đăng nhập trên trình duyệt, rồi bấm **Check** |
| "rejected the API key" | Key sai hoặc đã bị thu hồi. Settings > AI provider > dán lại key, **Test connection**, Save |
| "model ... was not found" | Tên model không còn dùng được. Settings > AI provider > **Load models**, chọn model khác, Save |
| "rate limit or quota is reached" | Tài khoản API hết hạn mức hoặc chưa bật thanh toán. Kiểm tra trên trang của nhà cung cấp |
| "Could not reach ..." | Không kết nối được tới dịch vụ. Kiểm tra mạng; mạng công ty có thể chặn địa chỉ đó |
| Máy không có Claude | Settings > AI provider > chọn OpenAI, Gemini hoặc Other |
| Nhấn hotkey không có gì xảy ra | App khác đang chiếm phím tắt. Settings > General > bấm vào ô Hotkey, nhấn tổ hợp mới, Save |
| Chọn sai style | Đổi trong popup bằng Ctrl+1..9, hoặc sửa ô **Apps** của style (Settings > Styles) |
| Setup báo "Constrained Language Mode" | Máy bị chính sách công ty chặn PowerShell Add-Type. Cần IT hỗ trợ |
| Sau khi cập nhật app Claude desktop, PolisK+ báo lỗi CLI | PolisK+ tự tìm lại `claude.exe`. Nếu vẫn lỗi: Settings > Advanced > **Browse...** chọn `claude.exe`, rồi Save |

---

## Dành cho người bảo trì (không nằm trong gói cài)

Thư mục nguồn:

1. `Polish.cs` (tray, hotkey, popup, gọi CLI), `Profiles.cs` (đọc/ghi style), `Providers.cs` (các AI provider, gọi API qua HTTPS, mã hoá API key), `SettingsForm.cs` (cửa sổ Settings). C# 5, `polish.ps1` biên dịch cả bốn trong bộ nhớ lúc chạy. Phiên bản nằm ở `App.Version`.
2. `polish.ps1 [-Settings]`: launcher. Nếu PolisK+ đang chạy, `-Settings` chỉ báo cho bản đang chạy mở Settings (qua event `Local\PolishTool.ShowSettings`).
3. `setup.ps1`, `uninstall.ps1`, `Setup.cmd`, `Uninstall.cmd`: cài đặt.
4. `prompt\rules.md`, `profiles\*.md`, `config.json`, `polish.ico`: mặc định được đóng gói. `tools\make-icon.ps1` vẽ lại icon.
5. `build-package.ps1`: tạo `dist\PolisK+-<version>.zip` (xoá `claudePath` và mọi `apiKeys`, kiểm tra script chỉ chứa ASCII).

Thêm một AI provider mới: khai báo trong `Providers.Build()`. Nếu API của nó tương thích OpenAI thì không cần viết thêm gì; nếu không, thêm một nhánh trong `Api.BuildPolish` và `Api.ParseText`.

Tên hiển thị là `PolisK+` (`App.AppName`). Tên nội bộ vẫn là `Polish` (thư mục cài, thư mục log, mutex, namespace) để nâng cấp từ bản cũ giữ nguyên cài đặt. Setup tự xoá các shortcut tên cũ "Polish".

Định dạng file style: dòng `description:`, `apps:`, `order:` ở đầu, sau đó hai mục `# Tone` và `# Output structure`.

Các file `.ps1` và `.cmd` phải chỉ chứa ký tự ASCII, vì Windows PowerShell 5.1 đọc file UTF-8 không BOM theo bảng mã ANSI.
