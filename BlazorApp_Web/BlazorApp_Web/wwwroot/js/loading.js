// 加载动画支持脚本
window.loadingHelpers = {
    // 隐藏页面加载动画
    hidePageLoading: function() {
        const loadingElement = document.getElementById('global-loading');
        if (loadingElement) {
            loadingElement.style.transition = 'all 0.5s ease-out';
            loadingElement.style.opacity = '0';
            loadingElement.style.visibility = 'hidden';
            
            setTimeout(() => {
                loadingElement.classList.remove('active');
            }, 500);
        }
    },

    // 显示导航加载条
    showNavigationLoading: function() {
        const topBar = document.getElementById('top-loading-bar');
        if (topBar) {
            topBar.classList.add('active');
        }
    },

    // 隐藏导航加载条
    hideNavigationLoading: function() {
        const topBar = document.getElementById('top-loading-bar');
        if (topBar) {
            setTimeout(() => {
                topBar.classList.remove('active');
            }, 300);
        }
    },

    // 显示全局加载动画
    showGlobalLoading: function(message = '正在处理...', subtitle = '请稍候') {
        const loadingElement = document.getElementById('global-loading');
        if (loadingElement) {
            // 更新文本内容
            const messageElement = loadingElement.querySelector('.loading-text h4');
            const subtitleElement = loadingElement.querySelector('.loading-subtitle');
            
            if (messageElement) messageElement.textContent = message;
            if (subtitleElement) subtitleElement.textContent = subtitle;
            
            // 显示加载动画
            loadingElement.classList.add('active');
            loadingElement.style.opacity = '1';
            loadingElement.style.visibility = 'visible';
        }
    },

    // 隐藏全局加载动画
    hideGlobalLoading: function() {
        this.hidePageLoading();
    },

    // 关闭错误提示
    dismissBlazorError: function() {
        const errorElement = document.getElementById('blazor-error-ui');
        if (errorElement) {
            errorElement.style.display = 'none';
        }
    },

    // 初始化加载动画
    initializeLoading: function() {
        // 监听页面可见性变化
        document.addEventListener('visibilitychange', function() {
            if (document.hidden) {
                // 页面隐藏时暂停动画
                document.body.style.animationPlayState = 'paused';
            } else {
                // 页面显示时恢复动画
                document.body.style.animationPlayState = 'running';
            }
        });

        // 监听网络连接状态
        window.addEventListener('online', function() {
            console.log('网络连接已恢复');
        });

        window.addEventListener('offline', function() {
            console.log('网络连接已断开');
            // 可以在这里显示离线提示
        });
    }
};

// 全局函数封装（向后兼容）
window.hidePageLoading = function() {
    window.loadingHelpers.hidePageLoading();
};

window.showGlobalLoading = function(message, subtitle) {
    window.loadingHelpers.showGlobalLoading(message, subtitle);
};

window.hideGlobalLoading = function() {
    window.loadingHelpers.hideGlobalLoading();
};

window.dismissBlazorError = function() {
    window.loadingHelpers.dismissBlazorError();
};

// 页面加载完成后初始化
document.addEventListener('DOMContentLoaded', function() {
    window.loadingHelpers.initializeLoading();
    
    // 预加载一些动画，确保流畅性
    const style = document.createElement('style');
    style.textContent = `
        /* 确保动画性能 */
        .drone-loading, .propeller, .loading-progress .progress-bar {
            will-change: transform, opacity;
        }
    `;
    document.head.appendChild(style);
});

// Blazor 集成
if (window.Blazor) {
    window.Blazor.addEventListener('enhancedload', function() {
        window.loadingHelpers.showNavigationLoading();
    });
    
    window.Blazor.addEventListener('enhancedloadcompleted', function() {
        window.loadingHelpers.hideNavigationLoading();
    });
} 