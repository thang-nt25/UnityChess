using System.Collections.Generic;

// Một lớp 'static' sẽ không bị hủy khi chuyển scene.
// Chúng ta dùng nó như một cái "hộp" để gửi đồ.
public static class ReplayManager
{
    // Biến này sẽ tạm thời giữ danh sách nước đi ("e2e4", "e7e5", v.v.)
    public static List<string> movesToReplay = null;
}