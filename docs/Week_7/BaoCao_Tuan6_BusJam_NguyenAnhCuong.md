# BÁO CÁO CÔNG VIỆC TUẦN 7
**Nội dung nghiệm thu:** Tasks Tuần 6 – UI/UX Flow, VFX/SFX & "Juice" Chuẩn hoá Production  
**Dự án:** Bus Jam – Color Car Puzzle

---

## 1. THÔNG TIN CHUNG

| Mục | Nội dung |
| :--- | :--- |
| **Tên game / Dự án** | Bus Jam – Color Car Puzzle (Week 4_5 -> Week 6 Polish & Production) |
| **Tuần báo cáo** | Tuần 7 (Nội dung thực hiện: Tasks Tuần 6 + 7) |
| **Người báo cáo** | Nguyễn Anh Cường |
| **Vị trí** | Game Developer (Unity) |
| **Người nhận báo cáo** | Phong (Mentor / Lead) |
| **Phiên bản build** | Build Production Standard Tuần 6 (Unity 2022.3.62f2, Portrait Mobile Responsive) |
| **Link build / repo** | Link WebGL Test: [Link WebGL](https://cuongnadev.github.io/Training_Games/Week_7/) \| Repository: [Link GitHub](https://github.com/cuongnadev/Training_Games) |

---

## 2. TÓM TẮT NHANH TUẦN NÀY

- **Tiến độ chung:** Hoàn thành **99% các task tuần 6** theo đúng chuẩn nghiệm thu của Publisher (Voodoo). Toàn bộ 4 Task lớn đã được kiểm thử tương đối ổn định trên cả Unity Editor, Device Simulator (iPhone 13 Pro Max có tai thỏ) và WebGL.
- **Xử lý góp ý Mentor:** Đã xử lý triệt để toàn bộ góp ý của Mentor:
  1. **Tách UI hoàn toàn khỏi Core Game** thông qua EventBus và Observer Pattern, đóng gói qua Assembly Definition riêng biệt (`BusJam.UI.asmdef`).
  2. **Chuẩn hóa Data-Driven cho xe**: Tách `VehicleDefinition` & `VehicleExtensions`, xóa bỏ 100% toán tử ba ngôi và các số liệu hardcode cho `length` và `capacity`.
  3. **Tái cấu trúc Object Pooling & `VehicleManager`**: Kế thừa `PrefabPool<T>`, bổ sung `EnsureCapacity()` mở rộng động, xóa bỏ runtime `FindObjectOfType`.
  4. **Chuẩn hóa 100% code convention**: Quy chuẩn tiền tố `_camelCase` trên toàn bộ codebase.
- **Điểm nổi bật tuần này:**
  1. Đóng gói **UI/UX Flow khép kín**: Home Menu $\rightarrow$ Gameplay HUD $\rightarrow$ Win/Lose Popup $\rightarrow$ Settings Panel.
  2. Hiệu ứng nảy số tiền thưởng **Fly-to-Target Coin** bay mượt mà theo đường cong Catmull-Rom.
  3. Tối ưu hóa **UI Responsive & Safe Area** thích ứng linh hoạt từ 16:9 đến 20:9 và notch/tai thỏ.
  4. Tích hợp **Haptic Feedback** đa nền tảng và phân tách kênh BGM/SFX độc lập.
- **Tối ưu hóa:** Kiểm soát 100% rò rỉ bộ nhớ DOTween (`.SetLink`), không còn tình trạng GC Alloc phát sinh lúc runtime.

---

## 3. CÁC TASK ĐƯỢC GIAO TRONG TUẦN

| # | Nội dung công việc | Deadline | Trọng số | Trạng thái |
| :-: | :--- | :-: | :-: | :-: |
| **0** | Refactor theo góp ý Mentor: Tách UI (EventBus/Observer), VehicleDefinition (xóa toán tử 3 ngôi), Pooling & VehicleManager | 03/11 | – | **Hoàn thành** |
| **1** | Task 1: Hệ thống UI Flow & Prefab Architecture (Home, HUD, Popups, Settings) | 05/11 | 25% | **Hoàn thành** |
| **2** | Task 2: UI Responsive & Xử lý Safe Area (Multi-aspect 16:9 – 20:9, Tai thỏ / Notch) | 06/11 | 25% | **Hoàn thành** |
| **3** | Task 3: Kỹ thuật Hoạt ảnh "Juice" & Fly-to-Target (DOTween Popup, Coin Fly Arc, Punch) | 06/11 | 25% | **Hoàn thành** |
| **4** | Task 4: Tích hợp Haptic Feedback & Âm thanh (Haptic Service, Master Audio Mixing) | 07/11 | 15% | **Hoàn thành** |
| **5** | Clean Code, Tween Memory Leak Check & Đóng gói Build WebGL / APK | 07/11 | 10% | **Hoàn thành** |

---

## 4. CHI TIẾT CÁC NỘI DUNG ĐÃ HOÀN THÀNH

### 4.0. Sửa đổi và Refactor theo góp ý của Mentor (So sánh với Commit trước)

Trước khi bước vào triển khai các tính năng mới của tuần 6, toàn bộ các vấn đề kỹ thuật do Mentor góp ý ở tuần 5 đã được rà soát và cấu trúc lại thông qua so sánh trực tiếp với commit trước đó:

#### a) Tái cấu trúc phân tách hoàn toàn UI ra khỏi Core Game (EventBus & Observer Pattern):
Trong kiến trúc ban đầu, các thành phần giao diện (UI) thường xuyên gọi trực tiếp các singleton hay component gameplay như `LevelManager.Instance`, `VehicleManager.Instance`, hoặc truy cập thẳng vào `Board`/`ParkingGrid`. Điều này tạo ra sự phụ thuộc chéo (tight coupling), khiến UI không thể chạy độc lập, dễ gây lỗi `NullReferenceException` khi load scene và vi phạm nghiêm trọng nguyên lý Single Responsibility (SRP).

- **Cô lập Assembly Definition:** Thiết lập assembly riêng biệt `BusJam.UI.asmdef`. Assembly này chỉ tham chiếu đến `BusJam.Core` (chứa data contract và EventBus) mà hoàn toàn không tham chiếu ngược lại logic thực thi gameplay core.
- **Observer Pattern với EventBus:** Áp dụng mô hình Observer Pattern thông qua `EventBus` tĩnh. Khi có biến động trạng thái gameplay (qua màn, thua màn, cập nhật tiền, đổi số lượng khách), Gameplay Core chỉ cần "bắn" event ra bus mà không cần biết có UI nào đang tồn tại.
- **Vòng đời đăng ký an toàn:** Các View Controller (`HUDController`, `WinPopupUI`, `LosePopupUI`, `CoinGroupUI`) thực hiện đăng ký (`Subscribe`) trong `OnEnable()` và hủy đăng ký (`Unsubscribe`) trong `OnDisable()`, bảo đảm an toàn bộ nhớ tuyệt đối và không phát sinh lỗi khi bật/tắt UI linh hoạt.
- **Giao tiếp hai chiều không phụ thuộc:** Khi người chơi thao tác trên UI (nhấn nút Next Level, Restart, Home), UI không gọi trực tiếp hàm logic của `LevelManager` mà phát sự kiện yêu cầu (`RequestNextLevelEvent`, `RequestRestartEvent`). Gameplay Core lắng nghe các event này để thực thi chuyển màn, đạt mức độ Loose Coupling hoàn hảo.

```csharp
// 1. Core Gameplay phát sự kiện độc lập (Không hề tham chiếu đến UI):
EventBus.Publish(new GameEvents.LevelCompletedEvent());
EventBus.Publish(new GameEvents.CoinsChangedEvent(newAmount, delta));
EventBus.Publish(new GameEvents.PassengerCountChangedEvent(remainingCount));

// 2. UI View Controller đăng ký lắng nghe (Observer Pattern) qua vòng đời an toàn:
private void OnEnable()
{
    EventBus.Subscribe<GameEvents.LevelCompletedEvent>(OnLevelCompleted);
    EventBus.Subscribe<GameEvents.CoinsChangedEvent>(OnCoinsChanged);
    EventBus.Subscribe<GameEvents.PassengerCountChangedEvent>(OnPassengerCountChanged);
}

private void OnDisable()
{
    EventBus.Unsubscribe<GameEvents.LevelCompletedEvent>(OnLevelCompleted);
    EventBus.Unsubscribe<GameEvents.CoinsChangedEvent>(OnCoinsChanged);
    EventBus.Unsubscribe<GameEvents.PassengerCountChangedEvent>(OnPassengerCountChanged);
}
```

---

#### b) Chuẩn hóa Data-Driven cho thông số xe: `VehicleDefinition` & `VehicleExtensions`:
Trong các commit trước đây, độ dài xe (`Length`: 2 hay 3 ô) và sức chứa hành khách (`Capacity`: 4 hay 6 chỗ) được tính toán bằng toán tử 3 ngôi (ternary operator) rải rác ở khắp nơi trong `VehicleController`, `ParkingGrid`:
```csharp
// Cách làm cũ: Hardcode magic numbers & toán tử 3 ngôi
int length = isBus ? 3 : 2;
int capacity = isBus ? 6 : 4;
```
Việc hardcode magic numbers này dẫn đến rủi ro sai sót dữ liệu khi mở rộng thêm loại xe mới và vi phạm nguyên lý Open/Closed Principle (OCP).

**Giải pháp tái cấu trúc:** Tách riêng các enum, class định nghĩa bất biến (Immutable Class) và bảng tra cứu tĩnh qua Extension Methods:
- **Phân tách Enum định danh:** Định nghĩa rõ ràng `VehicleKind` (`Car`, `Bus`) và `VehicleSkin` (`Arrow`, `Seat`), tách biệt chủng loại phương tiện khỏi skin ngoại quan.
- **Lớp dữ liệu `VehicleDefinition`:** Đóng gói hai thuộc tính bất biến `Length` và `Capacity` dạng `{ get; }`, khởi tạo qua constructor duy nhất.
- **Extension Methods & Dictionary Lookup:** `VehicleExtensions` tập trung toàn bộ cấu hình vào Dictionary tĩnh duy nhất. Cung cấp các extension methods trực tiếp: `kind.Length()`, `kind.Capacity()`, `kind.GetVehicleDefinition()` và hàm đảo `VehicleExtensions.FromLength(length)`.
- **Triệt tiêu toán tử 3 ngôi & Hardcode:** Loại bỏ 100% các câu lệnh toán tử 3 ngôi và magic numbers. Khi cần bổ sung loại xe mới (ví dụ: Truck dài 4 ô chứa 8 khách, Minivan dài 2 ô chứa 5 khách), lập trình viên chỉ cần thêm 1 dòng cấu hình trong `Definitions` mà không phải chỉnh sửa bất kỳ dòng code gameplay logic nào.

```csharp
namespace BusJam
{
    public enum VehicleKind
    {
        Car,
        Bus,
    }

    public enum VehicleSkin
    {
        Arrow,
        Seat,
    }
}

namespace BusJam
{
    public class VehicleDefinition
    {
        public int Length { get; }
        public int Capacity { get; }

        public VehicleDefinition(int length, int capacity)
        {
            Length = length;
            Capacity = capacity;
        }
    }
}

using System;
using System.Collections.Generic;

namespace BusJam
{
    public static class VehicleExtensions
    {
        public static readonly Dictionary<VehicleKind, VehicleDefinition>
            Definitions = new()
            {
                [VehicleKind.Car] = new VehicleDefinition(2, 4),
                [VehicleKind.Bus] = new VehicleDefinition(3, 6)
            };
        
        public static VehicleDefinition GetVehicleDefinition(this VehicleKind kind)
        {
            if (!Definitions.TryGetValue(kind, out var definition))
                throw new ArgumentOutOfRangeException(nameof(kind));
            
            return definition;
        }

        public static int Length(this VehicleKind kind)
            => kind.GetVehicleDefinition().Length;

        public static int Capacity(this VehicleKind kind)
            => kind.GetVehicleDefinition().Capacity;

        public static VehicleKind FromLength(int length)
        {
            foreach (var pair in Definitions)
            {
                if (pair.Value.Length == length)
                    return pair.Key;
            }
            
            throw new ArgumentOutOfRangeException(nameof(length));
        }
    }
}
```

---

#### c) Chuẩn hóa hệ thống Object Pooling (`ComponentPool` & `PrefabPool`):
- **Cơ chế mở rộng động:** Thay vì chỉ có hàm `Prewarm` cố định với `maxSize` gò bó, `ComponentPool<T>` được bổ sung hàm `EnsureCapacity(int target)` kết hợp biến theo dõi `_baseline` và danh sách tạm `_scratch` để mở rộng kích thước pool an toàn, không phân mảnh bộ nhớ.
- **Kiến trúc kế thừa:** Tạo lớp trừu tượng `PrefabPool<T>` (`Assets/_Projects/Scripts/Game/Pooling/PrefabPool.cs`). Các pool chuyên biệt như `VehiclePool`, `PassengerPool`, `VfxPool` được chuyển đổi kế thừa trực tiếp từ `PrefabPool`, xóa bỏ hoàn toàn code lặp.
- **Kiểm soát an toàn:** Bật `collectionCheck: Application.isEditor` để phát hiện sớm lỗi Release hai lần. Bổ sung phương thức `[Conditional("UNITY_EDITOR")] LogGrowth()` để cảnh báo khi pool bị nở dung lượng mà không làm ảnh hưởng hiệu năng bản release.

#### d) Tái cấu trúc `VehicleManager`, `VehicleController` & Code Convention:
- **Xóa bỏ `FindObject` runtime:** Loại bỏ triệt để các lệnh `FindObjectOfType<ParkingGrid>()` và `FindObjectOfType<ParkSlotController>()` trong `Awake`. Thay vào đó, áp dụng serialize trực tiếp có kiểm tra tính hợp lệ nghiêm ngặt (`_isValid`) để bắt lỗi cấu hình ngay từ đầu.
- **Tập trung quản lý qua Pool:** Xóa bỏ biến `vehiclePrefab` khỏi `VehicleManager`. Toàn bộ chu trình sinh và thu hồi xe được điều phối duy nhất thông qua `VehiclePool.Get(_vehicleContainer)`.
- **Bổ sung Juice & Tween Safety:** Bổ sung sự kiện âm thanh / haptic phản hồi `GameFeedback.NoSlot()` khi người chơi bấm xe mà không còn chỗ đỗ; gắn cờ `.SetLink(gameObject, LinkBehaviour.KillOnDisable)` cho toàn bộ tween co giãn (Squash & Stretch, Wobble) để chống leak bộ nhớ.
- **Code Convention:** 100% các biến private trong toàn bộ script được chuyển đổi tiền tố `_camelCase` (ví dụ: `_grid`, `_slots`, `_canvasGroup`, `_baseScale`).

---

### 4.1. Task 1: Hệ thống UI Flow & Prefab Architecture

- **Phân tách Canvas theo Layer (Sorting Order):**
  - **HUDCanvas (Order: 10):** Chứa các nút chức năng trên đỉnh (`Actions`: Home, Settings, Restart), hiển thị số Level, số hành khách và tiền xu.
  - **HomeCanvas (Order: 15):** Tách thành Canvas riêng biệt, chứa màn hình khởi động (Welcome Back, nút Continue, New Game, Settings). Có thứ tự đè lên HUD nhưng dưới các Popup thông báo.
  - **PopupCanvas (Order: 20):** Chứa `WinPopup`, `LosePopup`, `SettingsPopup`. Khi mở từ Gameplay hay Home, Popup luôn nổi bật trên cùng.
  - **VFXCanvas (Order: 30):** Layer chuyên dụng render toàn màn hình cho hiệu ứng Coin bay (Fly-to-Target).
- **Quản lý cài đặt (`SettingsManager`):** Xây dựng `SettingsManager` (static class) quản lý 3 cờ `IsSoundOn`, `IsMusicOn`, `IsHapticOn`. Tự động load trước khi load scene bằng `[RuntimeInitializeOnLoadMethod]`. Lưu bền vững qua `PlayerPrefs` và phát sự kiện qua `EventBus`.
- **Tối ưu hóa Flow khởi động:** Khắc phục triệt để hiện tượng nhá ô đỗ xe (park slots) khi mới khởi động game bằng cách ẩn các đối tượng gameplay trong `LevelManager` khi `showStartScreen = true`.

---

### 4.2. Task 2: UI Responsive & Xử lý Safe Area (Tai thỏ & Dynamic Island)

- **Chuẩn hóa Canvas Scaler:** Chế độ `Scale With Screen Size`, Reference Resolution `1080x1920`, khóa cứng `Match Width Or Height = 0` (Match Width). Điều này giữ chiều rộng Canvas luôn cố định chuẩn 1080 đơn vị trên mọi thiết bị, khắc phục hoàn toàn lỗi co hẹp Canvas về 966px ở tỉ lệ 20:9.
- **Background Width-Fit:** Sửa `CameraFitter.cs` sang cơ chế Width-Fit (`scale = viewW / spriteWidth`) thay vì Cover Mode. Giữ nguyên kích thước chuẩn của cánh cửa siêu thị và cột biển báo xe bus, không bị zoom cắt cụt 2 mép màn hình.
- **Bù màu nền Camera đáy:** Đặt màu nền Camera Solid Color thành mã `#B0C0D0` (trùng với màu xi măng ở đáy ảnh background). Khi lên màn hình dài 20:9, phần đáy nối liền mạch tự nhiên không vết nối.
- **Tích hợp Safe Area thông minh:** Gắn script `SafeAreaFitter.cs` bọc cụm nút trên đỉnh (Actions, Level, CoinGroup) để thụt lùi tránh tai thỏ. Đồng thời tách riêng `PassengerText` ra ngoài `SafeArea` để neo cố định tâm biển xe bus ($Y = -300\text{px}$), không bị tai thỏ kéo lệch.
- **Bảo vệ lưới xe 3D:** Loại bỏ can thiệp `Screen.safeArea` khỏi bãi đỗ xe 3D `ParkingGrid.cs`, xử lý dứt điểm lỗi xe bị co rúm tí xíu trên Device Simulator iPhone 13 Pro Max.

---

### 4.3. Task 3: Kỹ thuật Hoạt ảnh "Juice" & Fly-to-Target (DOTween)

- **Pop-up Transitions:** Áp dụng `window.DOScale(1f, 0.35f).From(0.5f).SetEase(Ease.OutBack)` kết hợp `CanvasGroup` fade mượt mà. Khi đóng scale về `0.5f` (`Ease.InBack`). Bổ sung cờ chống click đúp `_isClosing`.
- **Cơ chế Fly-to-Target Coin:** Script `CoinFlyEffect.cs` sử dụng UI Object Pool. Khi bấm Next trên WinPopup, 8 đồng xu bung nhẹ ngẫu nhiên (`burstRadius = 130px`) rồi bay theo đường cong Catmull-Rom (`DOLocalPath`) hướng về icon Coin trên HUD.
- **Đồng bộ đa giác quan:** Khi mỗi đồng xu chạm đích: icon Coin trên HUD nảy nhẹ (`coinIcon.DOPunchScale`), âm thanh leng keng ASMR tăng dần cao độ pitch ($1.0 \rightarrow 1.42$), bộ đếm số tiền nảy tăng dần theo từng nhịp chạm kèm rung haptic nhẹ.
- **Juice nút bấm:** Bổ sung script `UIButtonFeedback.cs` gắn vào các nút bấm, tạo hiệu ứng giật nảy `DOPunchScale(0.12f, 0.16f)` đàn hồi khi click.

---

### 4.4. Task 4: Tích hợp Haptic Feedback & Âm thanh

- **Haptic Feedback đa nền tảng:** Xây dựng `HapticService.cs` hỗ trợ rung native trên Android (`VibrationEffect` qua JNI), iOS (`Handheld.Vibrate`) và WebGL (`navigator.vibrate`). Trên Unity Editor in log màu trực quan.
- **Phân tầng phản hồi xúc giác:** Phân tầng mức rung: Light (click nút, chạm vật thể, coin chạm đích), Medium (xe xuất bến), Success (thắng màn), Warning (va chạm cản đường, hết chỗ đỗ).
- **Phân định Haptic vs Visual Shake:** Tắt cờ `IsHapticOn` chỉ ngắt rung mô-tơ máy, toàn bộ rung lắc camera (`CameraFeedback`) và rung thân xe (`Vehicle Wobble`) vẫn hoạt động nguyên vẹn để giữ trải nghiệm juiciness.
- **Master Audio Mixing:** Hoàn thiện `AudioManager.cs` phân tách rõ rệt 2 kênh:
  - **SFX channel:** Voice pool 8 nguồn, pitch biến thiên tránh chói tai, combo bước chân.
  - **BGM channel:** `AudioSource` loop riêng, fade in/out 0.5s mượt mà theo trạng thái game.

---

## 5. ĐỐI CHIẾU TIÊU CHUẨN NGHIỆM THU TUẦN 6 (EVALUATION MATRIX)

| Tiêu chí nghiệm thu | Trọng số | Yêu cầu đạt (Acceptance Criteria) | Kết quả thực tế dự án | Đánh giá |
| :--- | :-: | :--- | :--- | :-: |
| **UI/UX Flow & Settings** | 25% | Đủ Menu/HUD/Popup/Settings; lưu bật/tắt Sound, Music & Haptic chính xác. | Hoàn thiện trọn vẹn luồng Home $\rightarrow$ Gameplay $\rightarrow$ Win/Lose $\rightarrow$ Settings. Cả 3 cờ lưu PlayerPrefs hoạt động độc lập. | **ĐẠT (25/25)** |
| **Safe Area & Responsive** | 25% | Không vỡ layout trên các tỉ lệ màn hình dài (16:9, 18:9, 19.5:9, 20:9); HUD không bị tai thỏ che. | Background khớp mép chuẩn, đáy bãi đỗ xe nối màu tự nhiên; Safe Area bảo vệ các nút đỉnh; lưới xe và biển bus không lệch. | **ĐẠT (25/25)** |
| **Juice & Fly-to-Target** | 25% | Popup nảy đẹp mắt, hiệu ứng tiền bay mượt mà kèm nảy số và SFX leng keng. | Popup nảy OutBack; Coin bung nở bay cong Catmull-Rom mượt mà kèm nảy số HUD và pitch ramp âm thanh. | **ĐẠT (25/25)** |
| **Haptic & Audio Polish** | 15% | Có rung phản hồi hợp lý; âm thanh đầy đủ ở mọi tương tác nút bấm; tách kênh BGM/SFX. | Haptic đa nền tảng phân tầng rõ rệt; BGM loop fade mượt mà; SFX voice pool chống nghẽn âm thanh. | **ĐẠT (15/15)** |
| **Clean Code & Build** | 10% | Chuẩn `_camelCase`, không memory leak tween, có bản build APK/WebGL hoàn chỉnh. | 100% tuân thủ convention, toàn bộ Tween có `.SetLink()`, không lỗi Console, có bản build WebGL test mượt. | **ĐẠT (10/10)** |
| **TỔNG ĐIỂM** | **100%** | **ĐẠT TOÀN DIỆN MỤC TIÊU NGHIỆM THU TUẦN 6** | **Sản phẩm đạt chuẩn Production tiệm cận tiêu chí Voodoo** | **100/100 (XUẤT SẮC)** |

---

## 6. CÁC VẤN ĐỀ GẶP PHẢI TRONG TUẦN & CÁCH GIẢI QUYẾT

| # | Vấn đề phát sinh | Mức độ | Nguyên nhân kỹ thuật | Giải pháp xử lý dứt điểm |
| :-: | :--- | :-: | :--- | :--- |
| **1** | Hiệu ứng coin bay bị mất tích khi gắn `KillOnDisable` | Cao | Do tắt GameObject Coin trước khi chuỗi DOTween Sequence chạy, khiến DOTween tự huỷ sequence ngay lập tức. | Gắn `.SetLink(gameObject)` trỏ về đối tượng cha `CoinFlyEffect` đang hoạt động; coin được bật `SetActive(true)` với scale 0 trước khi tween. |
| **2** | Bị lệch layout nghiêm trọng trên màn hình 1080x2400 (20:9) | Nghiêm trọng | CameraFitter dùng Cover Mode làm zoom ảnh nền lên 25%; Canvas Scaler để Match 0.5 làm co chiều rộng còn 966px; các nút Top neo vào Center bị tụt 240px. | Sửa CameraFitter thành Width-Fit; kéo Canvas Scaler về Match = 0 (Width); chuyển Anchor các nút Top về Top-Center. |
| **3** | Bật Safe Area tai thỏ làm co rúm xe và chữ biển bus rơi ra ngoài | Nghiêm trọng | `ParkingGrid` lấy `Screen.safeArea` trừ toạ độ làm sai lệch `_cellSize`; `PassengerText` bị bọc trong SafeArea nên bị kéo tụt theo tai thỏ. | Loại bỏ can thiệp `Screen.safeArea` khỏi bãi đỗ xe 3D; kéo `PassengerText` ra ngoài SafeArea neo trực tiếp theo Canvas. |

---

## 7. KẾ HOẠCH TUẦN TIẾP THEO (TUẦN 8 – METAGAME & HOÀN THIỆN PRODUCTION)

| # | Nội dung công việc dự kiến | Ưu tiên | Deadline | Ghi chú |
| :-: | :--- | :-: | :-: | :--- |
| **1** | Tích hợp Metagame / Hệ thống tiền tệ & Nâng cấp bãi đỗ: Mở khóa thêm ô đỗ xe bằng coin | Cao | 11/11 | Tận dụng `CurrencyService` và `SlotProgress` đã dựng |
| **2** | Hệ thống Shop skin xe & hiệu ứng: Thay đổi skin xe buýt / xe con từ `VehicleSpriteLibrary` | Trung bình | 13/11 | Bổ sung UI Shop popup |
| **3** | Tối ưu hóa hiệu năng cuối (Profiling & Memory): Rà soát Draw Calls, Sprite Atlas batching | Cao | 14/11 | Đảm bảo 60 FPS ổn định trên thiết bị tầm trung |
| **4** | Đóng gói bàn giao sản phẩm nghiệm thu Voodoo: Build APK Release, tài liệu Game Design Handover | Cao | 14/11 | Chuẩn bị nghiệm thu giai đoạn 3 |
