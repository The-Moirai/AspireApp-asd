// 系统支持脚本
window.systemHelpers = {
    // 关闭错误提示
    dismissBlazorError: function() {
        const errorElement = document.getElementById('blazor-error-ui');
        if (errorElement) {
            errorElement.style.display = 'none';
        }
    },

    // 初始化系统
    initializeSystem: function() {
        console.log('初始化系统');

        // 监听页面可见性变化
        document.addEventListener('visibilitychange', function() {
            if (document.hidden) {
                console.log('页面已隐藏');
            } else {
                console.log('页面已显示');
            }
        });

        // 监听网络连接状态
        window.addEventListener('online', function() {
            console.log('网络连接已恢复');
        });

        window.addEventListener('offline', function() {
            console.log('网络连接已断开');
        });
    }
};

// 全局函数封装（向后兼容）
window.dismissBlazorError = function() {
    window.systemHelpers.dismissBlazorError();
};

// 页面加载完成后初始化
document.addEventListener('DOMContentLoaded', function() {
    window.systemHelpers.initializeSystem();
});

// 添加全局错误处理
window.addEventListener('error', function(e) {
    console.error('JavaScript错误:', e.error);
}); 