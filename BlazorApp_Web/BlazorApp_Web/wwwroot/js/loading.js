// 加载动画支持脚本
window.loadingHelpers = {
    // 隐藏页面加载动画
    hidePageLoading: function() {
        console.log('开始隐藏页面加载动画');
        const loadingElement = document.getElementById('global-loading');
        if (loadingElement) {
            loadingElement.style.transition = 'all 0.5s ease-out';
            loadingElement.style.opacity = '0';
            loadingElement.style.visibility = 'hidden';
            
            setTimeout(() => {
                loadingElement.classList.remove('active');
                loadingElement.style.display = 'none';
                console.log('加载动画已完全隐藏');
            }, 500);
        } else {
            console.warn('未找到加载动画元素');
        }
    },

    // 强制隐藏加载动画（立即生效）
    forceHidePageLoading: function() {
        console.log('强制隐藏页面加载动画');
        const loadingElement = document.getElementById('global-loading');
        if (loadingElement) {
            loadingElement.classList.remove('active');
            loadingElement.style.opacity = '0';
            loadingElement.style.visibility = 'hidden';
            loadingElement.style.display = 'none';
            loadingElement.style.transition = 'none';
            console.log('加载动画已强制隐藏');
        }
    },

    // 检查加载动画是否还在显示
    isLoadingVisible: function() {
        const loadingElement = document.getElementById('global-loading');
        if (!loadingElement) return false;
        
        const isActive = loadingElement.classList.contains('active');
        const isVisible = loadingElement.style.visibility !== 'hidden' && 
                         loadingElement.style.display !== 'none' && 
                         loadingElement.style.opacity !== '0';
        
        return isActive || isVisible;
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
            loadingElement.style.display = 'block';
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
        console.log('初始化加载动画系统');
        
        // 设置最大显示时间（10秒后强制隐藏）
        setTimeout(() => {
            if (this.isLoadingVisible()) {
                console.warn('加载动画显示时间过长，强制隐藏');
                this.forceHidePageLoading();
            }
        }, 10000);

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
        });

        // 页面完全加载后检查是否需要隐藏加载动画
        if (document.readyState === 'complete') {
            this.checkAndHideLoading();
        } else {
            window.addEventListener('load', () => {
                setTimeout(() => {
                    this.checkAndHideLoading();
                }, 2000);
            });
        }
    },

    // 检查并隐藏加载动画
    checkAndHideLoading: function() {
        // 如果页面已经完全加载但加载动画还在显示，就隐藏它
        if (document.readyState === 'complete' && this.isLoadingVisible()) {
            console.log('页面已加载完成，自动隐藏加载动画');
            setTimeout(() => {
                if (this.isLoadingVisible()) {
                    this.forceHidePageLoading();
                }
            }, 3000); // 3秒后再次检查
        }
    }
};

// 全局函数封装（向后兼容）
window.hidePageLoading = function() {
    window.loadingHelpers.hidePageLoading();
};

window.forceHidePageLoading = function() {
    window.loadingHelpers.forceHidePageLoading();
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

// 添加全局错误处理
window.addEventListener('error', function(e) {
    console.error('JavaScript错误:', e.error);
    // 如果出现错误且加载动画还在显示，强制隐藏
    if (window.loadingHelpers && window.loadingHelpers.isLoadingVisible()) {
        console.log('检测到错误，强制隐藏加载动画');
        window.loadingHelpers.forceHidePageLoading();
    }
}); 