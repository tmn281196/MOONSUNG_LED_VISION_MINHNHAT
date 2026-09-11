using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace LEDVision
{
    /// <summary>
    /// Cửa sổ hướng dẫn dùng chung, chọn theo chủ đề:
    ///   "roi" : thao tác ROI (vẽ / di chuyển / chọn nhóm / copy / paste / xóa / zoom)
    ///   "hsv" : lý thuyết màu HSV và cách app tách màu (segmentation) theo dải H/S/V
    /// Hỗ trợ 2 ngôn ngữ: English và ไทย, chuyển qua lại bằng nút ở góc phải trên (nhớ trong suốt phiên chạy).
    /// Nội dung được dựng từ bảng chuỗi bên dưới → thêm chủ đề / ngôn ngữ mới chỉ cần thêm một mục vào bảng.
    /// </summary>
    public partial class RoiHelpWindow : Window
    {
        // Ngôn ngữ đang chọn, giữ trong suốt phiên chạy (mở lại cửa sổ vẫn nhớ)
        private static string currentLang = "EN";

        private readonly string topic;

        private class Section
        {
            public string Title;
            public string[] Lines;
            public Section(string title, params string[] lines) { Title = title; Lines = lines; }
        }

        private class HelpText
        {
            public string WindowTitle;
            public string Heading;
            public string CloseLabel;
            public Section[] Sections;
        }

        // texts[topic][lang]
        private static readonly Dictionary<string, Dictionary<string, HelpText>> texts = new Dictionary<string, Dictionary<string, HelpText>>
        {
            // ======================= ROI =======================
            {
                "roi", new Dictionary<string, HelpText>
                {
                    {
                        "EN", new HelpText
                        {
                            WindowTitle = "ROI Help",
                            Heading = "ROI editing guide",
                            CloseLabel = "Close",
                            Sections = new[]
                            {
                                new Section("1. Choose a group",
                                    "In Inspection Settings → LED Group, pick a group in the drop-down. Groups are free: add as many as you need (Add), rename (Rename) or remove them (Delete).",
                                    "Every ROI you draw goes into the selected group. Each group has its own HSV range, ROI radius and area threshold.",
                                    "A VISION CHECK step on the Sequence page checks one group by its name.",
                                    "Press None to lock the image (no drawing / moving)."),
                                new Section("2. Add an ROI",
                                    "RIGHT-click on the camera image.",
                                    "A circular ROI is created there with the current Radius (ROI Size slider).",
                                    "Area Threshold sets the minimum lit area for that group."),
                                new Section("3. Move one ROI",
                                    "Drag the ROI with the LEFT mouse button.",
                                    "Or click the ROI once, then press the arrow keys ← ↑ → ↓ to nudge it 1 px at a time."),
                                new Section("4. Select and move many ROIs (Group Select)",
                                    "Click the Group Select button (the group icon) to enter group mode.",
                                    "LEFT-drag on an empty area to draw a rectangle around the ROIs you want.",
                                    "LEFT-drag inside that rectangle to move the whole selection.",
                                    "Arrow keys also move the whole selection. In this mode you cannot add or drag single ROIs.",
                                    "Click Group Select again to return to single-ROI mode."),
                                new Section("5. Copy / Paste / Duplicate",
                                    "Ctrl+C : copy the selected ROI (or the group selection).",
                                    "Ctrl+V : paste into the current group at a new position (each paste shifts 20 px further).",
                                    "After pasting, the pasted copies become the current selection, not the originals.",
                                    "Ctrl+D : duplicate the selected ROI right next to the original."),
                                new Section("6. Delete",
                                    "Delete : remove the selected ROI (or the group selection).",
                                    "Ctrl+Delete : remove every ROI of the current group.",
                                    "Ctrl+Shift+Delete : remove all ROIs of all groups."),
                                new Section("7. Zoom the view",
                                    "Roll the mouse wheel over the image to zoom in / out around the cursor.",
                                    "Reset Zoom button (magnifier icon) restores 1:1."),
                                new Section("8. Tips",
                                    "Keyboard shortcuts work anywhere on the Vision page, except while typing in a text box.",
                                    "After editing, press the Save button at the top-right to write the model file, otherwise changes are lost.",
                                    "Camera settings (exposure, focus, shift...) are saved into the same model file."),
                            }
                        }
                    },
                    {
                        "TH", new HelpText
                        {
                            WindowTitle = "คู่มือ ROI",
                            Heading = "คู่มือการแก้ไข ROI",
                            CloseLabel = "ปิด",
                            Sections = new[]
                            {
                                new Section("1. เลือกกลุ่ม",
                                    "ที่แท็บ Inspection Settings → LED Group เลือกกลุ่มจากช่องเลือก กลุ่มเพิ่มได้ไม่จำกัด (Add) เปลี่ยนชื่อได้ (Rename) หรือลบได้ (Delete)",
                                    "ROI ทุกอันที่วาดจะถูกเพิ่มเข้ากลุ่มที่เลือกอยู่ แต่ละกลุ่มมีช่วง HSV รัศมี ROI และ Area Threshold ของตัวเอง",
                                    "step VISION CHECK ในหน้า Sequence จะตรวจกลุ่มตามชื่อ",
                                    "กด None เพื่อล็อกภาพ (วาด / ย้ายไม่ได้)"),
                                new Section("2. เพิ่ม ROI",
                                    "คลิกขวาที่ภาพจากกล้อง",
                                    "จะสร้าง ROI วงกลมที่จุดนั้นด้วยขนาดรัศมีปัจจุบัน (แถบเลื่อน Radius ในส่วน ROI Size)",
                                    "Area Threshold คือพื้นที่สว่างขั้นต่ำที่ถือว่าติดของกลุ่มนั้น"),
                                new Section("3. ย้าย ROI ทีละอัน",
                                    "ลาก ROI ด้วยเมาส์ซ้าย",
                                    "หรือคลิก ROI หนึ่งครั้ง แล้วกดปุ่มลูกศร ← ↑ → ↓ เพื่อขยับทีละ 1 พิกเซล"),
                                new Section("4. เลือกและย้ายหลาย ROI (Group Select)",
                                    "กดปุ่ม Group Select (ไอคอนรูปกลุ่ม) เพื่อเข้าโหมดเลือกหลายอัน",
                                    "ลากเมาส์ซ้ายบนพื้นที่ว่างเพื่อวาดกรอบสี่เหลี่ยมล้อม ROI ที่ต้องการ",
                                    "ลากเมาส์ซ้ายภายในกรอบนั้นเพื่อย้าย ROI ทั้งหมดที่เลือกไว้พร้อมกัน",
                                    "ปุ่มลูกศรก็ใช้ย้ายทั้งกลุ่มได้ ในโหมดนี้จะเพิ่มหรือลาก ROI ทีละอันไม่ได้",
                                    "กด Group Select อีกครั้งเพื่อกลับสู่โหมดเลือกทีละอัน"),
                                new Section("5. คัดลอก / วาง / ทำซ้ำ",
                                    "Ctrl+C : คัดลอก ROI ที่เลือก (หรือกลุ่มที่เลือกไว้)",
                                    "Ctrl+V : วางลงในกลุ่มปัจจุบันที่ตำแหน่งใหม่ (วางแต่ละครั้งเลื่อนออกไปอีก 20 พิกเซล)",
                                    "หลังวาง ROI ที่วางใหม่จะกลายเป็นตัวที่ถูกเลือก ไม่ใช่ต้นฉบับ",
                                    "Ctrl+D : ทำซ้ำ ROI ที่เลือกไว้ข้าง ๆ ต้นฉบับทันที"),
                                new Section("6. ลบ",
                                    "Delete : ลบ ROI ที่เลือก (หรือกลุ่มที่เลือกไว้)",
                                    "Ctrl+Delete : ลบ ROI ทั้งหมดของกลุ่มปัจจุบัน",
                                    "Ctrl+Shift+Delete : ลบ ROI ทั้งหมดของทุกกลุ่ม"),
                                new Section("7. ซูมภาพ",
                                    "หมุนล้อเมาส์บนภาพเพื่อซูมเข้า / ออก รอบตำแหน่งเมาส์",
                                    "ปุ่ม Reset Zoom (ไอคอนแว่นขยาย) กลับสู่ขนาด 1:1"),
                                new Section("8. เคล็ดลับ",
                                    "คีย์ลัดใช้ได้ทุกที่ในหน้า Vision ยกเว้นตอนกำลังพิมพ์ในช่องข้อความ",
                                    "หลังแก้ไขให้กดปุ่ม Save ที่มุมขวาบนเพื่อบันทึกไฟล์โมเดล มิฉะนั้นการแก้ไขจะหายไป",
                                    "ค่าตั้งกล้อง (exposure, focus, shift...) ถูกบันทึกลงไฟล์โมเดลเดียวกัน"),
                            }
                        }
                    },
                }
            },

            // ======================= AUTO =======================
            {
                "auto", new Dictionary<string, HelpText>
                {
                    {
                        "EN", new HelpText
                        {
                            WindowTitle = "Auto Test Help",
                            Heading = "Auto test page guide",
                            CloseLabel = "Close",
                            Sections = new[]
                            {
                                new Section("1. Starting a test",
                                    "Normally the test starts by itself when the cylinder reaches the down position (down sensor on the IO Box).",
                                    "START runs a test by hand, for example when no trigger is wired. The app must be on this page and a model must be open.",
                                    "Signals arriving while a test is running, or within 1.5 s after it ends, are ignored so the cylinder movement of the app itself cannot retrigger."),
                                new Section("2. EMERGENCY STOP",
                                    "While a test runs, START turns into the red EMERGENCY STOP.",
                                    "Press it at any step: the cylinder stops where it is, LED power is cut, the banner shows READY and nothing is counted.",
                                    "The same happens automatically if the cylinder leaves the down position during a test."),
                                new Section("3. Status banner",
                                    "READY : waiting for a trigger.  TESTING : LEDs powered and the camera is checking.",
                                    "PASS : every ROI matched its colour range in every sample.  NG : at least one ROI failed even after the retests.",
                                    "On NG an image of the camera view is saved in the log folder (see Setting page)."),
                                new Section("4. Counters",
                                    "TOTAL / PASS / FAIL count finished tests; cancelled tests are not counted.",
                                    "They are saved to disk on every change and restored when the app starts.",
                                    "Hover over TOTAL to reveal the red CLEAR button, which resets all three."),
                                new Section("5. CYLINDER and Used Pins",
                                    "CYLINDER shows the down sensor: DOWN (green) = cylinder fully down, UP (blue) = not down, dash = COM not connected.",
                                    "Used Pins counts test cycles on the current pogo pins. When it reaches Max set a warning appears; replace the pins and reset it on the Setting page."),
                                new Section("6. Top bar icons",
                                    "Camera icon : green = camera running; click to stop / start it.",
                                    "Chip icon : green = IO Box connected; click to disconnect / connect.",
                                    "The model name in the title is the model currently loaded; Save writes changes to it, Save As writes a new file."),
                            }
                        }
                    },
                    {
                        "TH", new HelpText
                        {
                            WindowTitle = "คู่มือ Auto Test",
                            Heading = "คู่มือหน้า Auto test",
                            CloseLabel = "ปิด",
                            Sections = new[]
                            {
                                new Section("1. การเริ่มทดสอบ",
                                    "ปกติการทดสอบจะเริ่มเองเมื่อกระบอกสูบลงถึงตำแหน่งล่าง (เซ็นเซอร์ล่างบนIO Box)",
                                    "START ใช้เริ่มทดสอบด้วยมือ เช่น กรณีไม่ได้ต่อสัญญาณทริกเกอร์ ต้องอยู่ที่หน้านี้และเปิดโมเดลไว้แล้ว",
                                    "สัญญาณที่มาระหว่างทดสอบ หรือภายใน 1.5 วินาทีหลังจบ จะถูกละเว้น เพื่อไม่ให้การเคลื่อนที่ของกระบอกสูบที่แอปสั่งเองไปทริกเกอร์ซ้ำ"),
                                new Section("2. EMERGENCY STOP",
                                    "ระหว่างทดสอบ ปุ่ม START จะกลายเป็น EMERGENCY STOP สีแดง",
                                    "กดได้ทุกขั้นตอน: กระบอกสูบหยุดตรงที่อยู่ ตัดไฟ LED แถบสถานะแสดง READY และไม่นับผล",
                                    "จะเกิดเหมือนกันโดยอัตโนมัติถ้ากระบอกสูบออกจากตำแหน่งล่างระหว่างทดสอบ"),
                                new Section("3. แถบสถานะ",
                                    "READY : รอทริกเกอร์  TESTING : จ่ายไฟ LED แล้วและกล้องกำลังตรวจ",
                                    "PASS : ROI ทุกอันตรงช่วงสีในทุกตัวอย่าง  NG : มี ROI อย่างน้อยหนึ่งอันไม่ผ่านแม้ทดสอบซ้ำแล้ว",
                                    "เมื่อ NG ภาพจากกล้องจะถูกบันทึกในโฟลเดอร์ log (ดูหน้า Setting)"),
                                new Section("4. ตัวนับ",
                                    "TOTAL / PASS / FAIL นับการทดสอบที่จบแล้ว การทดสอบที่ถูกยกเลิกไม่นับ",
                                    "บันทึกลงดิสก์ทุกครั้งที่เปลี่ยนและโหลดกลับเมื่อเปิดแอป",
                                    "เลื่อนเมาส์ไปที่ TOTAL จะเห็นปุ่ม CLEAR สีแดง ใช้รีเซ็ตทั้งสามค่า"),
                                new Section("5. CYLINDER และ Used Pins",
                                    "CYLINDER แสดงเซ็นเซอร์ล่าง: DOWN (เขียว) = กระบอกสูบลงสุด, UP (น้ำเงิน) = ไม่ได้อยู่ล่าง, ขีด = ยังไม่ต่อ COM",
                                    "Used Pins นับรอบทดสอบของพินสปริงชุดปัจจุบัน เมื่อถึง Max set จะมีคำเตือน ให้เปลี่ยนพินแล้วรีเซ็ตในหน้า Setting"),
                                new Section("6. ไอคอนบนแถบด้านบน",
                                    "ไอคอนกล้อง : เขียว = กล้องทำงานอยู่ คลิกเพื่อหยุด / เริ่ม",
                                    "ไอคอนชิป : เขียว = IO Boxเชื่อมต่ออยู่ คลิกเพื่อตัด / เชื่อมต่อ",
                                    "ชื่อโมเดลในหัวเรื่องคือโมเดลที่โหลดอยู่ Save เขียนทับไฟล์นั้น Save As เขียนไฟล์ใหม่"),
                            }
                        }
                    },
                }
            },

            // ======================= SEQUENCE =======================
            {
                "sequence", new Dictionary<string, HelpText>
                {
                    {
                        "EN", new HelpText
                        {
                            WindowTitle = "Test Sequence Help",
                            Heading = "Test sequence guide",
                            CloseLabel = "Close",
                            Sections = new[]
                            {
                                new Section("1. What the sequence is",
                                    "The list of steps the Auto page runs at every test, from top to bottom, once the cylinder is down and the Before sequence delay has passed.",
                                    "Every step reports PASS or FAIL. One FAIL makes the whole test NG (then the retest rules of the Setting page apply).",
                                    "The list is part of the model: press Save on the top bar after editing, otherwise the Auto page keeps running the last saved list.",
                                    "At the end of a test (PASS, NG or EMERGENCY STOP) the app switches the LED power and all relays OFF by itself."),
                                new Section("2. Commands",
                                    "POWER  ON / OFF : the product 220 V relay. Timeout (optional) = wait this many ms after switching. The IO Box only allows ON while the cylinder is down.",
                                    "RELAY  ON / OFF : one of the five general-purpose relays. Target = relay number 1 to 5; leave Target empty to switch all five. Timeout (optional) = wait this many ms after switching.",
                                    "DELAY : wait. Spec = time in ms (e.g. 1000).",
                                    "VISION CHECK : the camera checks one LED group. Target = group name (Vision page → LED Group). Timeout = sampling time in ms (default 2000, one sample every 100 ms). PASS only if every ROI of the group is lit with the right colour in every scored sample; Persist warm-up samples are not scored."),
                                new Section("3. Columns",
                                    "No : order of execution. CMD / Sub CMD : the command and ON / OFF.",
                                    "Target : relay number (RELAY) or group name (VISION CHECK).",
                                    "Spec : DELAY time in ms.  Timeout : sampling time (VISION CHECK) or wait after switching (POWER / RELAY).",
                                    "Comment : free text, shown on the Auto page.  Skip : ticked steps are not executed (result shows a dash)."),
                                new Section("4. Editing",
                                    "Fill the fields on the right and press ADD STEP: the step is inserted after the selected row (at the end if none is selected).",
                                    "Select a row to load it into the fields, change them, then press UPDATE STEP.",
                                    "You can also type directly in a cell of the table.",
                                    "Move Up / Down / First / Last reorder the selected step; Duplicate copies it; Delete removes it; Delete All clears the list.",
                                    "Default replaces the list with POWER ON → DELAY 1000 → then for every group: RELAY n ON → VISION CHECK → RELAY n OFF (relay 1 for the first group, relay 2 for the second...) → POWER OFF."),
                                new Section("5. Typical sequence (multiplexed LED board: one relay powers one LED group)",
                                    "1) POWER ON  then  DELAY 1000",
                                    "2) RELAY ON  Target = 1, Timeout 300   → relay 1 powers the common of group 1",
                                    "3) VISION CHECK  Target = 4-LED, Timeout 2000",
                                    "4) RELAY OFF  Target = 1",
                                    "5) RELAY ON  Target = 2, Timeout 300  →  VISION CHECK  Target = 7-Segment  →  RELAY OFF  Target = 2",
                                    "6) RELAY ON  Target = 3, Timeout 300  →  VISION CHECK  Target = Decimal Point  →  RELAY OFF  Target = 3",
                                    "7) POWER OFF",
                                    "The app also switches power and relays off by itself when the test ends or is stopped, so nothing stays on after an NG."),
                            }
                        }
                    },
                    {
                        "TH", new HelpText
                        {
                            WindowTitle = "คู่มือลำดับการทดสอบ",
                            Heading = "หน้า Sequence",
                            CloseLabel = "ปิด",
                            Sections = new[]
                            {
                                new Section("1. ลำดับการทดสอบคืออะไร",
                                    "รายการ step ที่หน้า Auto จะรันทุกครั้งที่ทดสอบ จากบนลงล่าง หลังกระบอกลงสุดและครบเวลา Before sequence",
                                    "ทุก step จะรายงาน PASS หรือ FAIL มี FAIL เพียงหนึ่ง = การทดสอบทั้งรอบเป็น NG (จากนั้นใช้กฎทดสอบซ้ำในหน้า Setting)",
                                    "รายการนี้เป็นส่วนหนึ่งของโมเดล แก้แล้วต้องกด Save บนแถบด้านบน ไม่เช่นนั้นหน้า Auto จะยังรันรายการที่บันทึกไว้ล่าสุด",
                                    "เมื่อจบการทดสอบ (PASS, NG หรือ EMERGENCY STOP) แอปจะปิดไฟ LED และรีเลย์ทั้งหมดให้เอง"),
                                new Section("2. คำสั่ง",
                                    "POWER  ON / OFF : รีเลย์ไฟ 220 V ของชิ้นงาน Timeout (ไม่บังคับ) = รอกี่ ms หลังสั่ง IO Box ยอมให้ ON เฉพาะตอนกระบอกอยู่ล่างเท่านั้น",
                                    "RELAY  ON / OFF : รีเลย์อเนกประสงค์ 1 ใน 5 ตัว Target = หมายเลขรีเลย์ 1 ถึง 5 เว้นว่าง = สั่งทั้ง 5 ตัว Timeout (ไม่บังคับ) = รอกี่ ms หลังสั่ง",
                                    "DELAY : รอ Spec = เวลาเป็น ms (เช่น 1000)",
                                    "VISION CHECK : กล้องตรวจกลุ่ม LED หนึ่งกลุ่ม Target = ชื่อกลุ่ม (หน้า Vision → LED Group) Timeout = เวลาเก็บตัวอย่างเป็น ms (ค่าเริ่มต้น 2000 เก็บทุก 100 ms) PASS ก็ต่อเมื่อ ROI ทุกอันของกลุ่มติดถูกสีในทุกตัวอย่างที่ให้คะแนน ตัวอย่างช่วงอุ่นเครื่องของ Persist ไม่ถูกให้คะแนน"),
                                new Section("3. คอลัมน์",
                                    "No : ลำดับการรัน CMD / Sub CMD : คำสั่งและ ON / OFF",
                                    "Target : หมายเลขรีเลย์ (RELAY) หรือชื่อกลุ่ม (VISION CHECK)",
                                    "Spec : เวลา DELAY เป็น ms  Timeout : เวลาเก็บตัวอย่าง (VISION CHECK) หรือเวลารอหลังสั่ง (POWER / RELAY)",
                                    "Comment : ข้อความอิสระ แสดงในหน้า Auto  Skip : step ที่ติ๊กจะไม่ถูกรัน (ผลแสดงเป็นขีด)"),
                                new Section("4. การแก้ไข",
                                    "กรอกช่องด้านขวาแล้วกด ADD STEP: step จะถูกแทรกต่อจากแถวที่เลือก (ต่อท้ายถ้าไม่ได้เลือก)",
                                    "เลือกแถวเพื่อโหลดค่าเข้าช่อง แก้ไข แล้วกด UPDATE STEP",
                                    "พิมพ์แก้ในช่องของตารางโดยตรงก็ได้",
                                    "Move Up / Down / First / Last ย้ายลำดับ step ที่เลือก Duplicate คัดลอก Delete ลบ Delete All ล้างทั้งรายการ",
                                    "Default แทนที่รายการด้วย POWER ON → DELAY 1000 → แล้วสำหรับทุกกลุ่ม: RELAY n ON → VISION CHECK → RELAY n OFF (รีเลย์ 1 สำหรับกลุ่มแรก รีเลย์ 2 สำหรับกลุ่มที่สอง...) → POWER OFF"),
                                new Section("5. ตัวอย่างลำดับทั่วไป (บอร์ด LED แบบสแกน: รีเลย์ 1 ตัวจ่ายไฟ LED 1 กลุ่ม)",
                                    "1) POWER ON  แล้ว  DELAY 1000",
                                    "2) RELAY ON  Target = 1, Timeout 300   → รีเลย์ 1 จ่ายไฟขาร่วมของกลุ่ม 1",
                                    "3) VISION CHECK  Target = 4-LED, Timeout 2000",
                                    "4) RELAY OFF  Target = 1",
                                    "5) RELAY ON  Target = 2, Timeout 300  →  VISION CHECK  Target = 7-Segment  →  RELAY OFF  Target = 2",
                                    "6) RELAY ON  Target = 3, Timeout 300  →  VISION CHECK  Target = Decimal Point  →  RELAY OFF  Target = 3",
                                    "7) POWER OFF",
                                    "แอปจะปิดไฟและรีเลย์ให้เองด้วยเมื่อการทดสอบจบหรือถูกหยุด จึงไม่มีอะไรค้าง ON หลัง NG"),
                            }
                        }
                    },
                }
            },

            // ======================= SETTING =======================
            {
                "setting", new Dictionary<string, HelpText>
                {
                    {
                        "EN", new HelpText
                        {
                            WindowTitle = "Settings Help",
                            Heading = "Settings page guide",
                            CloseLabel = "Close",
                            Sections = new[]
                            {
                                new Section("1. IO Box",
                                    "COM port : the serial port of the IO Box. Press the refresh icon after plugging the board in.",
                                    "Connect / Disconnect : opens or closes the port. Green dot = connected, red = failed, yellow = not tried.",
                                    "The chip icon on the top bar does the same thing and shows the live status (green = connected)."),
                                new Section("2. Log Directory",
                                    "Folder where an image of the camera view is saved every time a test ends NG (file name = date and time).",
                                    "Browse opens the Windows folder picker."),
                                new Section("3. Retest Management",
                                    "Retest times : how many extra attempts are made when a test is NG. 0 = never retest. Example: 1 = at most 2 tests per product.",
                                    "Before sequence (ms) : after the down sensor reports the cylinder is down, wait this long before the test sequence starts (lets vibration settle). Used at the first test and at every retest.",
                                    "The sequence itself (LED power on, delays, relays, camera checks) is defined on the Sequence page and saved in the model.",
                                    "Delay between UP / DOWN (ms) : during a retest the cylinder is raised then lowered again. After the down sensor releases, this is the extra time given for the cylinder to finish going up before it is sent down.",
                                    "Sensor timeout (ms) : maximum time to wait for the down sensor to report the cylinder is fully down (at test start and after the retest lowering) or has left the bottom (retest raising). If it expires the sequence continues anyway; the board still refuses to power the LEDs unless the cylinder is down, so the result will be NG rather than unsafe. Typical value 2000 to 4000."),
                                new Section("4. Pin Replacement",
                                    "Current : number of test cycles done with the current pogo pins (read-only, increases after each finished test).",
                                    "Max set : when Current reaches this number a warning appears, meaning the pins should be replaced.",
                                    "After replacing the pins, reset Current on the Setting page."),
                                new Section("5. Saving",
                                    "Press SAVE SETTING to write everything above to setting.json. Unsaved values are lost when the app closes.",
                                    "PASS / FAIL counters and the last opened model are saved automatically in the same file."),
                            }
                        }
                    },
                    {
                        "TH", new HelpText
                        {
                            WindowTitle = "คู่มือ Settings",
                            Heading = "คู่มือหน้า Settings",
                            CloseLabel = "ปิด",
                            Sections = new[]
                            {
                                new Section("1. IO Box",
                                    "COM port : พอร์ตอนุกรมของIO Box เสียบบอร์ดแล้วกดไอคอนรีเฟรชเพื่อค้นหาพอร์ต",
                                    "Connect / Disconnect : เปิดหรือปิดพอร์ต จุดเขียว = เชื่อมต่อแล้ว, แดง = ล้มเหลว, เหลือง = ยังไม่ได้ลอง",
                                    "ไอคอนชิปบนแถบด้านบนทำงานเหมือนกันและแสดงสถานะสด (เขียว = เชื่อมต่ออยู่)"),
                                new Section("2. Log Directory",
                                    "โฟลเดอร์ที่บันทึกภาพจากกล้องทุกครั้งที่ผลทดสอบเป็น NG (ชื่อไฟล์ = วันที่และเวลา)",
                                    "ปุ่ม Browse เปิดหน้าต่างเลือกโฟลเดอร์ของ Windows"),
                                new Section("3. Retest Management",
                                    "Retest times : จำนวนครั้งที่ทดสอบซ้ำเพิ่มเมื่อผลเป็น NG 0 = ไม่ทดสอบซ้ำ ตัวอย่าง 1 = ทดสอบได้สูงสุด 2 ครั้งต่อชิ้นงาน",
                                    "Before sequence (ms) : หลังเซ็นเซอร์ล่างรายงานว่ากระบอกลงสุดแล้ว รอเท่านี้ก่อนเริ่มลำดับการทดสอบ (ให้แรงสั่นสงบ) ใช้ทั้งตอนทดสอบครั้งแรกและทุกครั้งที่ทดสอบซ้ำ",
                                    "ตัวลำดับการทดสอบเอง (จ่ายไฟ LED, หน่วงเวลา, รีเลย์, กล้องตรวจ) กำหนดในหน้า Sequence และบันทึกในโมเดล",
                                    "Delay between UP / DOWN (ms) : ตอนทดสอบซ้ำ กระบอกสูบจะยกขึ้นแล้วลงใหม่ หลังเซ็นเซอร์ล่างปล่อยแล้ว นี่คือเวลาเพิ่มให้กระบอกสูบขึ้นจนสุดก่อนสั่งลง",
                                    "Sensor timeout (ms) : เวลาสูงสุดที่รอเซ็นเซอร์ล่างรายงานว่ากระบอกสูบลงสุด (ตอนเริ่มทดสอบและหลังสั่งลงตอนทดสอบซ้ำ) หรือออกจากตำแหน่งล่าง (ตอนยกขึ้นตอนทดสอบซ้ำ) ถ้าหมดเวลาลำดับงานจะดำเนินต่อ บอร์ดจะยังไม่จ่ายไฟ LED ถ้ากระบอกสูบไม่ลง ผลจึงเป็น NG ไม่ใช่อันตราย ค่าที่ใช้ทั่วไป 2000 ถึง 4000"),
                                new Section("4. Pin Replacement",
                                    "Current : จำนวนรอบทดสอบที่ใช้กับพินสปริงชุดปัจจุบัน (อ่านอย่างเดียว เพิ่มทุกครั้งที่ทดสอบเสร็จ)",
                                    "Max set : เมื่อ Current ถึงค่านี้จะมีคำเตือน หมายถึงควรเปลี่ยนพิน",
                                    "หลังเปลี่ยนพินแล้วให้รีเซ็ต Current ในหน้า Setting"),
                                new Section("5. การบันทึก",
                                    "กด SAVE SETTING เพื่อบันทึกทุกค่าด้านบนลง setting.json ค่าที่ไม่ได้บันทึกจะหายเมื่อปิดโปรแกรม",
                                    "ตัวนับ PASS / FAIL และโมเดลที่เปิดล่าสุดถูกบันทึกอัตโนมัติในไฟล์เดียวกัน"),
                            }
                        }
                    },
                }
            },

            // ======================= HSV =======================
            {
                "hsv", new Dictionary<string, HelpText>
                {
                    {
                        "EN", new HelpText
                        {
                            WindowTitle = "HSV Help",
                            Heading = "HSV colour segmentation",
                            CloseLabel = "Close",
                            Sections = new[]
                            {
                                new Section("1. What is HSV?",
                                    "The camera delivers pixels as Red / Green / Blue. The app converts every frame to HSV before inspecting.",
                                    "H (Hue) = the colour tone: red, yellow, green, cyan, blue, magenta... independent of how bright it is.",
                                    "S (Saturation) = how pure the colour is. 0 = grey / white, 255 = fully vivid.",
                                    "V (Value) = how bright the pixel is. 0 = black, 255 = brightest.",
                                    "HSV is used because an LED's Hue stays almost the same when exposure or ambient light changes, while RGB values all move together."),
                                new Section("2. Ranges used by this app (OpenCV scale)",
                                    "H : 0 ~ 179 (half of the 0 ~ 359° colour wheel).  S : 0 ~ 255.  V : 0 ~ 255.",
                                    "Approximate Hue of common LED colours: Red 0 (and 175 ~ 179), Orange 10 ~ 20, Yellow 25 ~ 35, Green 55 ~ 75, Cyan 85 ~ 95, Blue 110 ~ 130, Magenta / Pink 145 ~ 165.",
                                    "Red sits at BOTH ends of the scale. The app does not wrap, so pick one side: either 0 ~ 10 or 170 ~ 179, whichever the histogram shows."),
                                new Section("3. How the inspection works",
                                    "For each ROI the app keeps only pixels whose H, S and V all fall inside the Set ranges of that ROI's group (inRange → mask).",
                                    "It then counts the kept pixels (the lit area). If the count is greater than Area Threshold, the LED is judged ON with the right colour → PASS; otherwise → NG.",
                                    "Persist (Inspection Settings): a pixel counts as lit only after it has stayed lit for the set time (ms). In Auto test the warm-up samples are not scored, so a flicker never gives a false PASS."),
                                new Section("4. Reading the panel",
                                    "Set : the min ~ max range for the group currently ticked. Click a number box to pick the value with the colour picker.",
                                    "Dotted circle row : the measured min ~ max of the SELECTED ROI (dashed outline on the image, orange line in the histogram).",
                                    "Solid circle row : the measured min ~ max over the UNSELECTED ROIs of the group (blue line in the histogram).",
                                    "All row : the measured min ~ max over ALL ROIs of the group (numbers only, not drawn in the histogram).",
                                    "H / S / V histogram tabs : how many pixels of the selected ROIs have each value. The tall peak is the LED colour; set min and max a little either side of that peak."),
                                new Section("5. How to set the ranges",
                                    "1) Turn the LED on, draw the ROI, then open the H tab: put Hue min / max just around the main peak (about ±8).",
                                    "2) S tab: an LED that is on is vivid, so start with S min ≈ 80 ~ 120 and S max = 255. White LEDs are the exception: S is low, use 0 ~ 60.",
                                    "3) V tab: an LED that is on is bright, so V min ≈ 100 ~ 150 and V max = 255. V min is what rejects an LED that is OFF.",
                                    "4) Turn the LED off and check that the lit area drops below Area Threshold. Raise V min or Area Threshold if it does not.",
                                    "5) Check the other colours in the same group do not fall inside this Hue range. Narrow the range if they do."),
                                new Section("6. Common problems",
                                    "Colours look white / washed out and S is near 0 → the camera is over-exposed. Lower Exposure or Gain in Camera Settings.",
                                    "Everything is dark and V is low → raise Exposure, or lower V min.",
                                    "Two colours overlap in Hue (e.g. orange vs yellow) → narrow both ranges and give each its own group.",
                                    "Reflections from the housing count as lit pixels → shrink the ROI radius or raise Area Threshold.",
                                    "Result flickers between PASS and NG → enable Persist with about 300 ~ 500 ms."),
                            }
                        }
                    },
                    {
                        "TH", new HelpText
                        {
                            WindowTitle = "คู่มือ HSV",
                            Heading = "การแยกสีด้วย HSV",
                            CloseLabel = "ปิด",
                            Sections = new[]
                            {
                                new Section("1. HSV คืออะไร",
                                    "กล้องส่งพิกเซลมาเป็นค่า แดง / เขียว / น้ำเงิน (RGB) แอปจะแปลงทุกเฟรมเป็น HSV ก่อนตรวจ",
                                    "H (Hue) = โทนสี เช่น แดง เหลือง เขียว ฟ้า น้ำเงิน ม่วง โดยไม่ขึ้นกับความสว่าง",
                                    "S (Saturation) = ความสดของสี 0 = เทา / ขาว, 255 = สีสดเต็มที่",
                                    "V (Value) = ความสว่างของพิกเซล 0 = ดำ, 255 = สว่างที่สุด",
                                    "ที่ใช้ HSV เพราะค่า Hue ของ LED แทบไม่เปลี่ยนเมื่อแสงหรือ exposure เปลี่ยน ในขณะที่ค่า RGB จะเปลี่ยนไปพร้อมกันทั้งหมด"),
                                new Section("2. ช่วงค่าที่แอปใช้ (สเกลของ OpenCV)",
                                    "H : 0 ~ 179 (ครึ่งหนึ่งของวงล้อสี 0 ~ 359°)  S : 0 ~ 255  V : 0 ~ 255",
                                    "ค่า Hue โดยประมาณของสี LED ที่พบบ่อย: แดง 0 (และ 175 ~ 179), ส้ม 10 ~ 20, เหลือง 25 ~ 35, เขียว 55 ~ 75, ฟ้า 85 ~ 95, น้ำเงิน 110 ~ 130, ม่วง / ชมพู 145 ~ 165",
                                    "สีแดงอยู่ทั้งสองปลายของสเกล แอปไม่วนค่า ให้เลือกด้านเดียว: 0 ~ 10 หรือ 170 ~ 179 ตามที่ฮิสโทแกรมแสดง"),
                                new Section("3. การตรวจทำงานอย่างไร",
                                    "ในแต่ละ ROI แอปจะเก็บเฉพาะพิกเซลที่ค่า H, S และ V ทั้งสามอยู่ในช่วง Set ของกลุ่มนั้น (inRange → mask)",
                                    "จากนั้นนับจำนวนพิกเซลที่เหลือ (พื้นที่สว่าง) ถ้ามากกว่า Area Threshold ถือว่า LED ติดและสีถูกต้อง → PASS ไม่เช่นนั้น → NG",
                                    "Persist (ใน Inspection Settings): พิกเซลจะนับว่าสว่างก็ต่อเมื่อสว่างต่อเนื่องครบเวลาที่ตั้ง (ms) ในโหมด Auto ตัวอย่างช่วงอุ่นเครื่องจะไม่ถูกให้คะแนน จึงไม่เกิด PASS ปลอมจากการกะพริบ"),
                                new Section("4. อ่านค่าบนแผง",
                                    "Set : ช่วง min ~ max ของกลุ่มที่ติ๊กอยู่ คลิกช่องตัวเลขเพื่อเลือกค่าจากตัวเลือกสี",
                                    "แถววงกลมเส้นประ : ค่า min ~ max ที่วัดได้จาก ROI ที่เลือกอยู่ (ROI ที่มีเส้นขอบประบนภาพ เส้นสีส้มในฮิสโทแกรม)",
                                    "แถววงกลมเส้นทึบ : ค่า min ~ max ที่วัดได้จาก ROI ที่ยังไม่ได้เลือกของกลุ่ม (เส้นสีน้ำเงินในฮิสโทแกรม)",
                                    "แถว All : ค่า min ~ max ที่วัดได้จาก ROI ทั้งหมดของกลุ่ม (แสดงเฉพาะตัวเลข ไม่วาดในฮิสโทแกรม)",
                                    "แท็บฮิสโทแกรม H / S / V : จำนวนพิกเซลของ ROI ที่เลือกในแต่ละค่า ยอดสูงคือสีของ LED ให้ตั้ง min และ max เผื่อไว้เล็กน้อยสองข้างของยอดนั้น"),
                                new Section("5. วิธีตั้งช่วงค่า",
                                    "1) เปิด LED วาด ROI แล้วเปิดแท็บ H ตั้ง Hue min / max ล้อมรอบยอดหลัก (ประมาณ ±8)",
                                    "2) แท็บ S: LED ที่ติดจะมีสีสด เริ่มที่ S min ≈ 80 ~ 120 และ S max = 255 ยกเว้น LED สีขาวที่ S ต่ำ ให้ใช้ 0 ~ 60",
                                    "3) แท็บ V: LED ที่ติดจะสว่าง ตั้ง V min ≈ 100 ~ 150 และ V max = 255 ค่า V min คือตัวคัด LED ที่ดับออก",
                                    "4) ปิด LED แล้วตรวจว่าพื้นที่สว่างลดต่ำกว่า Area Threshold ถ้าไม่ ให้เพิ่ม V min หรือ Area Threshold",
                                    "5) ตรวจว่าสีอื่นในกลุ่มเดียวกันไม่ตกอยู่ในช่วง Hue นี้ ถ้าตกให้บีบช่วงให้แคบลง"),
                                new Section("6. ปัญหาที่พบบ่อย",
                                    "สีดูขาว / ซีด และ S ใกล้ 0 → กล้องรับแสงมากเกินไป ให้ลด Exposure หรือ Gain ใน Camera Settings",
                                    "ภาพมืดทั้งหมดและ V ต่ำ → เพิ่ม Exposure หรือลด V min",
                                    "สองสีมี Hue ทับกัน (เช่น ส้มกับเหลือง) → บีบช่วงของทั้งคู่ให้แคบและแยกคนละกลุ่ม",
                                    "แสงสะท้อนจากตัวเครื่องถูกนับเป็นพิกเซลสว่าง → ลดรัศมี ROI หรือเพิ่ม Area Threshold",
                                    "ผลกะพริบสลับ PASS / NG → เปิด Persist ประมาณ 300 ~ 500 ms"),
                            }
                        }
                    },
                }
            },
        };

        public RoiHelpWindow() : this("roi")
        {
        }

        public RoiHelpWindow(string topic)
        {
            InitializeComponent();
            this.topic = texts.ContainsKey(topic) ? topic : "roi";
            Render();
        }

        private void LangEn_Click(object sender, RoutedEventArgs e)
        {
            currentLang = "EN";
            Render();
        }

        private void LangTh_Click(object sender, RoutedEventArgs e)
        {
            currentLang = "TH";
            Render();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // Dựng lại toàn bộ nội dung theo chủ đề + ngôn ngữ đang chọn
        private void Render()
        {
            var byLang = texts[topic];
            HelpText t;
            if (!byLang.TryGetValue(currentLang, out t)) t = byLang["EN"];

            Title = t.WindowTitle;
            titleText.Text = t.Heading;
            btnClose.Content = t.CloseLabel;
            btnEn.Style = (Style)FindResource(currentLang == "EN" ? "LangBtnActive" : "LangBtn");
            btnTh.Style = (Style)FindResource(currentLang == "TH" ? "LangBtnActive" : "LangBtn");

            contentPanel.Children.Clear();
            var titleBrush = new SolidColorBrush(Color.FromRgb(0x32, 0x3F, 0x4E));
            var bodyBrush = new SolidColorBrush(Color.FromRgb(0x2B, 0x2B, 0x2B));

            if (topic == "hsv")
            {
                contentPanel.Children.Add(BuildHueBar());
            }
            if (topic == "setting" || topic == "auto" || topic == "sequence")
            {
                contentPanel.Children.Add(BuildTestTimeline(currentLang));
            }

            foreach (var sec in t.Sections)
            {
                contentPanel.Children.Add(new TextBlock
                {
                    Text = sec.Title,
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Foreground = titleBrush,
                    Margin = new Thickness(0, 10, 0, 4),
                });

                foreach (var line in sec.Lines)
                {
                    var tb = new TextBlock
                    {
                        FontSize = 13,
                        Foreground = bodyBrush,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(12, 1, 0, 1),
                    };
                    tb.Inlines.Add(new Run("•  "));
                    tb.Inlines.Add(new Run(line));
                    contentPanel.Children.Add(tb);
                }
            }
        }

        // Thanh màu Hue 0 → 179 (đỏ → vàng → lục → cyan → xanh → magenta → đỏ) kèm vạch số theo thang OpenCV
        // ---- Timeline chuỗi test: mỗi ô là một bước, ô có viền đậm là bước có thể chỉnh ở trang Setting ----
        private class Step
        {
            public string Label;      // tên bước
            public string Sub;        // ô setting / nguồn thời gian
            public Color Color;
            public bool Adjustable;   // chỉnh được ở Setting → viền đậm
            public Step(string label, string sub, Color color, bool adjustable) { Label = label; Sub = sub; Color = color; Adjustable = adjustable; }
        }

        private static UIElement BuildTestTimeline(string lang)
        {
            bool th = lang == "TH";
            var cSensor = Color.FromRgb(0x80, 0xC4, 0xE9);   // chờ cảm biến
            var cDelay = Color.FromRgb(0xE1, 0xE5, 0xEA);    // chờ theo setting
            var cPower = Color.FromRgb(0xF5, 0xA6, 0x23);    // bật nguồn
            var cCheck = Color.FromRgb(0x06, 0xC7, 0x55);    // camera kiểm tra
            var cMove = Color.FromRgb(0x32, 0x3F, 0x4E);     // xi lanh chuyển động

            var first = new[]
            {
                new Step(th ? "ทริกเกอร์" : "Trigger", th ? "เซ็นเซอร์ล่าง / START" : "down sensor / START", cSensor, false),
                new Step(th ? "รอลงสุด" : "Wait fully down", "Sensor timeout", cSensor, true),
                new Step(th ? "หน่วง" : "Delay", "Before sequence", cDelay, true),
                new Step(th ? "รันลำดับ" : "Run sequence", th ? "POWER / RELAY / DELAY / VISION CHECK (หน้า Sequence)" : "POWER / RELAY / DELAY / VISION CHECK (Sequence page)", cCheck, true),
                new Step(th ? "ผล" : "Result", th ? "PASS / NG → ทดสอบซ้ำ?" : "PASS / NG → retest?", cSensor, false),
            };
            var retry = new[]
            {
                new Step(th ? "ตัดไฟ + ยกขึ้น" : "Power OFF + UP", th ? "รอเซ็นเซอร์ปล่อย" : "wait sensor release", cMove, false),
                new Step(th ? "หน่วง" : "Delay", "Delay between UP / DOWN", cDelay, true),
                new Step(th ? "สั่งลง" : "DOWN", "Sensor timeout", cSensor, true),
                new Step(th ? "หน่วง" : "Delay", "Before sequence", cDelay, true),
                new Step(th ? "รันลำดับ" : "Run sequence", th ? "ลำดับเดียวกับครั้งแรก" : "same steps as the first test", cCheck, true),
            };

            var root = new StackPanel { Margin = new Thickness(0, 6, 0, 4) };
            root.Children.Add(new TextBlock
            {
                Text = th ? "ลำดับการทดสอบ (กรอบเข้ม = ตั้งค่าได้ในหน้านี้)" : "Test sequence (bold border = adjustable on this page)",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x32, 0x3F, 0x4E)),
                Margin = new Thickness(0, 0, 0, 4),
            });
            root.Children.Add(BuildTimelineRow(th ? "ทดสอบครั้งแรก" : "First test", first));
            root.Children.Add(BuildTimelineRow(th ? "ทดสอบซ้ำ (เมื่อ NG, สูงสุด Retest times ครั้ง)" : "Retest (when NG, up to Retest times)", retry));
            root.Children.Add(new TextBlock
            {
                Text = th ? "EMERGENCY STOP หรือกระบอกสูบออกจากตำแหน่งล่างระหว่างทาง → ยกเลิกทันที ไม่นับผล"
                          : "EMERGENCY STOP or the cylinder leaving the down position at any step → cancelled immediately, nothing counted",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x6B, 0x7A, 0x8C)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 6),
            });
            return root;
        }

        private static UIElement BuildTimelineRow(string title, Step[] steps)
        {
            var row = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
            row.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0x6B, 0x7A, 0x8C)),
                Margin = new Thickness(0, 0, 0, 2),
            });
            var grid = new Grid();
            for (int i = 0; i < steps.Length; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                if (i < steps.Length - 1) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            }
            var ink = new SolidColorBrush(Color.FromRgb(0x32, 0x3F, 0x4E));
            for (int i = 0; i < steps.Length; i++)
            {
                var st = steps[i];
                bool darkBg = st.Color == Color.FromRgb(0x32, 0x3F, 0x4E) || st.Color == Color.FromRgb(0x06, 0xC7, 0x55) || st.Color == Color.FromRgb(0xF5, 0xA6, 0x23);
                var fg = darkBg ? Brushes.White : ink;
                var box = new Border
                {
                    Background = new SolidColorBrush(st.Color),
                    BorderBrush = ink,
                    BorderThickness = new Thickness(st.Adjustable ? 2 : 0),
                    Padding = new Thickness(4, 5, 4, 5),
                    MinHeight = 48,
                };
                var inner = new StackPanel();
                inner.Children.Add(new TextBlock { Text = st.Label, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = fg, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
                if (!string.IsNullOrEmpty(st.Sub))
                    inner.Children.Add(new TextBlock { Text = st.Sub, FontSize = 10, Foreground = fg, Opacity = 0.9, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 2, 0, 0) });
                box.Child = inner;
                Grid.SetColumn(box, i * 2);
                grid.Children.Add(box);
                if (i < steps.Length - 1)
                {
                    var arrow = new TextBlock { Text = "\u25B6", FontSize = 9, Foreground = ink, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 2, 0) };
                    Grid.SetColumn(arrow, i * 2 + 1);
                    grid.Children.Add(arrow);
                }
            }
            row.Children.Add(grid);
            return row;
        }

        private static UIElement BuildHueBar()
        {
            var panel = new StackPanel { Margin = new Thickness(0, 6, 0, 4) };

            var gradient = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            gradient.GradientStops.Add(new GradientStop(Color.FromRgb(255, 0, 0), 0.0));       // H 0    red
            gradient.GradientStops.Add(new GradientStop(Color.FromRgb(255, 255, 0), 1.0 / 6)); // H 30   yellow
            gradient.GradientStops.Add(new GradientStop(Color.FromRgb(0, 255, 0), 2.0 / 6));   // H 60   green
            gradient.GradientStops.Add(new GradientStop(Color.FromRgb(0, 255, 255), 3.0 / 6)); // H 90   cyan
            gradient.GradientStops.Add(new GradientStop(Color.FromRgb(0, 0, 255), 4.0 / 6));   // H 120  blue
            gradient.GradientStops.Add(new GradientStop(Color.FromRgb(255, 0, 255), 5.0 / 6)); // H 150  magenta
            gradient.GradientStops.Add(new GradientStop(Color.FromRgb(255, 0, 0), 1.0));       // H 179  red

            panel.Children.Add(new Border
            {
                Height = 22,
                Background = gradient,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xC9, 0xD1, 0xDB)),
                BorderThickness = new Thickness(1),
            });

            var ticks = new Grid { Margin = new Thickness(0, 2, 0, 0) };
            string[] labels = { "0", "30", "60", "90", "120", "150", "179" };
            for (int i = 0; i < labels.Length; i++)
            {
                ticks.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var tb = new TextBlock
                {
                    Text = labels[i],
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x6B, 0x7A, 0x8C)),
                    HorizontalAlignment = i == 0 ? HorizontalAlignment.Left : (i == labels.Length - 1 ? HorizontalAlignment.Right : HorizontalAlignment.Center),
                };
                Grid.SetColumn(tb, i);
                ticks.Children.Add(tb);
            }
            panel.Children.Add(ticks);
            panel.Children.Add(new TextBlock
            {
                Text = "H (Hue) 0 ~ 179  ·  S (Saturation) 0 ~ 255  ·  V (Value) 0 ~ 255",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x6B, 0x7A, 0x8C)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0),
            });
            return panel;
        }
    }
}
