# Changelog

Tất cả thay đổi đáng chú ý của Spine Asset Studio được ghi lại trong file này.

Định dạng dựa trên [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) và dự án tuân theo [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-08-20

### Added

- Package metadata cho `com.raven12345.spine-asset-studio`.
- Hướng dẫn cài package trực tiếp từ Git trong README.
- Cửa sổ hướng dẫn khi spine-unity chưa được cài.
- Nút tự kiểm tra spine-unity và thêm scripting define `SPINE_UNITY` cho build target hiện tại.

### Changed

- Spine dependency là tùy chọn ở thời điểm import package, nên project không bị lỗi compile khi chưa có spine-unity.
- Bỏ assembly reference cứng tới `Spine.Unity` để hỗ trợ spine-unity được import bằng các cấu hình assembly khác nhau.
