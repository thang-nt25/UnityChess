// File: Assets/Scripts/Utils/TaskExtensions.cs
// Đảm bảo file này nằm trong thư mục Scripts và không có namespace để dễ truy cập

using System;
using System.Threading.Tasks;

public static class TaskExtensions
{
    /// <summary>
    /// Cho phép Task hoàn thành trong khoảng thời gian timeout nhất định, nếu không sẽ ném ra ngoại lệ TimeoutException.
    /// </summary>
    public static async Task<T> TimeoutAfter<T>(this Task<T> task, int millisecondsTimeout)
    {
        if (task.IsCompleted) return task.Result;

        // Tạo một Task sẽ hoàn thành sau thời gian timeout
        var timer = Task.Delay(millisecondsTimeout);

        // Chờ Task ban đầu HOẶC timer hoàn thành
        if (await Task.WhenAny(task, timer) == timer)
        {
            throw new TimeoutException("The UCI operation timed out.");
        }

        // Nếu task hoàn thành trước timer, trả về kết quả hoặc ném lỗi nếu task lỗi.
        return await task;
    }
}