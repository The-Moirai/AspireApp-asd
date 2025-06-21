#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
优化后的图片传输测试脚本
测试连接池、并发传输、重试机制等优化功能
"""

import os
import sys
import time
import random
import threading
from PIL import Image
import numpy as np

# 导入优化后的传输函数
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from real_work import (
    send_images_to_mission_service, 
    connection_pool, 
    MAX_CONCURRENT_UPLOADS,
    DEBUG_MODE,
    LOG_NETWORK_STATS
)

def create_test_image(filename: str, size_kb: int) -> int:
    """创建指定大小的测试图片"""
    # 计算图片尺寸以达到目标文件大小
    target_bytes = size_kb * 1024
    # 估算：RGB图片每像素约3字节，加上PNG压缩比约50%
    pixels_needed = int((target_bytes * 2) / 3)
    width = int(np.sqrt(pixels_needed))
    height = pixels_needed // width
    
    # 创建随机图片数据
    image_array = np.random.randint(0, 256, (height, width, 3), dtype=np.uint8)
    
    # 保存为PNG
    image = Image.fromarray(image_array)
    image.save(filename, 'PNG')
    
    # 返回实际文件大小
    return os.path.getsize(filename)

def test_single_image_transmission():
    """测试单张图片传输"""
    print("=" * 60)
    print("🧪 测试1: 单张图片传输优化")
    print("=" * 60)
    
    test_file = "test_optimized_single.png"
    actual_size = create_test_image(test_file, 50)  # 50KB测试文件
    
    try:
        task_id = "test-task-single-optimized"
        subtask_id = "test-subtask-single-optimized"
        
        print(f"📋 测试信息:")
        print(f"   TaskId: {task_id}")
        print(f"   SubTaskId: {subtask_id}")
        print(f"   文件: {test_file} ({actual_size:,} 字节)")
        
        start_time = time.time()
        success = send_images_to_mission_service(task_id, subtask_id, [test_file])
        end_time = time.time()
        
        if success:
            print(f"✅ 单张图片传输测试成功，耗时: {end_time - start_time:.2f} 秒")
            return True
        else:
            print("❌ 单张图片传输测试失败")
            return False
            
    except Exception as e:
        print(f"❌ 单张图片传输测试异常: {e}")
        return False
    finally:
        if os.path.exists(test_file):
            os.remove(test_file)

def test_multiple_images_transmission():
    """测试多张图片并发传输"""
    print("\n" + "=" * 60)
    print("🧪 测试2: 多张图片并发传输")
    print("=" * 60)
    
    # 创建不同大小的测试图片
    test_files = []
    sizes_kb = [10, 25, 50, 75, 100]  # 不同大小的测试文件
    
    try:
        for i, size_kb in enumerate(sizes_kb):
            test_file = f"test_multi_optimized_{i+1:02d}.png"
            actual_size = create_test_image(test_file, size_kb)
            test_files.append(test_file)
            print(f"📁 创建测试文件: {test_file} ({actual_size:,} 字节)")
        
        task_id = "test-task-multi-optimized"
        subtask_id = "test-subtask-multi-optimized"
        
        print(f"\n📋 测试信息:")
        print(f"   TaskId: {task_id}")
        print(f"   SubTaskId: {subtask_id}")
        print(f"   文件数量: {len(test_files)} 张")
        print(f"   并发数: {MAX_CONCURRENT_UPLOADS}")
        
        start_time = time.time()
        success = send_images_to_mission_service(task_id, subtask_id, test_files)
        end_time = time.time()
        
        if success:
            print(f"✅ 多张图片并发传输测试成功，耗时: {end_time - start_time:.2f} 秒")
            return True
        else:
            print("❌ 多张图片并发传输测试失败")
            return False
            
    except Exception as e:
        print(f"❌ 多张图片并发传输测试异常: {e}")
        return False
    finally:
        # 清理测试文件
        for test_file in test_files:
            if os.path.exists(test_file):
                os.remove(test_file)

def test_large_file_transmission():
    """测试大文件传输"""
    print("\n" + "=" * 60)
    print("🧪 测试3: 大文件传输优化")
    print("=" * 60)
    
    test_file = "test_large_optimized.png"
    actual_size = create_test_image(test_file, 2048)  # 2MB大文件
    
    try:
        task_id = "test-task-large-optimized"
        subtask_id = "test-subtask-large-optimized"
        
        print(f"📋 测试信息:")
        print(f"   TaskId: {task_id}")
        print(f"   SubTaskId: {subtask_id}")
        print(f"   文件: {test_file} ({actual_size:,} 字节 = {actual_size/1024/1024:.2f} MB)")
        
        start_time = time.time()
        success = send_images_to_mission_service(task_id, subtask_id, [test_file])
        end_time = time.time()
        
        duration = end_time - start_time
        if success and duration > 0:
            speed_mbps = (actual_size / 1024 / 1024) / duration
            print(f"✅ 大文件传输测试成功")
            print(f"   耗时: {duration:.2f} 秒")
            print(f"   传输速度: {speed_mbps:.2f} MB/s")
            return True
        else:
            print("❌ 大文件传输测试失败")
            return False
            
    except Exception as e:
        print(f"❌ 大文件传输测试异常: {e}")
        return False
    finally:
        if os.path.exists(test_file):
            os.remove(test_file)

def test_connection_pool_reuse():
    """测试连接池复用"""
    print("\n" + "=" * 60)
    print("🧪 测试4: 连接池复用测试")
    print("=" * 60)
    
    test_files = []
    
    try:
        # 创建多个小文件进行快速传输测试
        for i in range(10):
            test_file = f"test_pool_{i+1:02d}.png"
            create_test_image(test_file, 5)  # 5KB小文件
            test_files.append(test_file)
        
        # 清空连接池统计
        connection_pool.stats = {
            'created': 0,
            'reused': 0,
            'closed': 0,
            'errors': 0
        }
        
        task_id = "test-task-pool-optimized"
        subtask_id = "test-subtask-pool-optimized"
        
        print(f"📋 连接池复用测试:")
        print(f"   文件数量: {len(test_files)} 张")
        print(f"   期望连接复用率: > 50%")
        
        start_time = time.time()
        success = send_images_to_mission_service(task_id, subtask_id, test_files)
        end_time = time.time()
        
        # 获取连接池统计
        stats = connection_pool.get_stats()
        total_connections = stats['created'] + stats['reused']
        reuse_rate = (stats['reused'] / total_connections * 100) if total_connections > 0 else 0
        
        print(f"\n📊 连接池统计:")
        print(f"   创建连接: {stats['created']}")
        print(f"   复用连接: {stats['reused']}")
        print(f"   关闭连接: {stats['closed']}")
        print(f"   连接错误: {stats['errors']}")
        print(f"   复用率: {reuse_rate:.1f}%")
        print(f"   当前池大小: {stats['pool_size']}")
        print(f"   活跃连接: {stats['active_connections']}")
        
        if success and reuse_rate > 30:  # 期望至少30%的复用率
            print(f"✅ 连接池复用测试成功，复用率: {reuse_rate:.1f}%")
            return True
        else:
            print(f"❌ 连接池复用测试失败，复用率过低: {reuse_rate:.1f}%")
            return False
            
    except Exception as e:
        print(f"❌ 连接池复用测试异常: {e}")
        return False
    finally:
        # 清理测试文件
        for test_file in test_files:
            if os.path.exists(test_file):
                os.remove(test_file)

def test_retry_mechanism():
    """测试重试机制（模拟网络不稳定）"""
    print("\n" + "=" * 60)
    print("🧪 测试5: 重试机制测试")
    print("=" * 60)
    
    # 注意：这个测试需要手动断开网络或停止服务来模拟失败
    print("⚠️  重试机制测试需要手动模拟网络故障")
    print("   建议：在传输过程中短暂断开网络连接")
    print("   或者临时停止MissionSocketService服务")
    
    test_file = "test_retry_optimized.png"
    actual_size = create_test_image(test_file, 100)  # 100KB文件
    
    try:
        task_id = "test-task-retry-optimized"
        subtask_id = "test-subtask-retry-optimized"
        
        print(f"\n📋 重试测试信息:")
        print(f"   TaskId: {task_id}")
        print(f"   SubTaskId: {subtask_id}")
        print(f"   文件: {test_file} ({actual_size:,} 字节)")
        print(f"   最大重试次数: 5")
        print(f"   指数退避延迟: 是")
        
        print("\n🚀 开始传输（请在传输过程中模拟网络故障）...")
        
        start_time = time.time()
        success = send_images_to_mission_service(task_id, subtask_id, [test_file])
        end_time = time.time()
        
        if success:
            print(f"✅ 重试机制测试成功，最终传输成功，耗时: {end_time - start_time:.2f} 秒")
            return True
        else:
            print("❌ 重试机制测试失败，最终传输失败")
            return False
            
    except Exception as e:
        print(f"❌ 重试机制测试异常: {e}")
        return False
    finally:
        if os.path.exists(test_file):
            os.remove(test_file)

def test_concurrent_stress():
    """并发压力测试"""
    print("\n" + "=" * 60)
    print("🧪 测试6: 并发压力测试")
    print("=" * 60)
    
    # 创建大量小文件进行并发传输
    test_files = []
    
    try:
        num_files = 20  # 20个文件
        for i in range(num_files):
            test_file = f"test_stress_{i+1:02d}.png"
            create_test_image(test_file, random.randint(10, 100))  # 10-100KB随机大小
            test_files.append(test_file)
        
        task_id = "test-task-stress-optimized"
        subtask_id = "test-subtask-stress-optimized"
        
        total_size = sum(os.path.getsize(f) for f in test_files)
        
        print(f"📋 并发压力测试:")
        print(f"   文件数量: {len(test_files)} 张")
        print(f"   总大小: {total_size:,} 字节 ({total_size/1024/1024:.2f} MB)")
        print(f"   并发线程: {MAX_CONCURRENT_UPLOADS}")
        
        start_time = time.time()
        success = send_images_to_mission_service(task_id, subtask_id, test_files)
        end_time = time.time()
        
        duration = end_time - start_time
        if success and duration > 0:
            throughput = len(test_files) / duration
            speed_mbps = (total_size / 1024 / 1024) / duration
            print(f"✅ 并发压力测试成功")
            print(f"   耗时: {duration:.2f} 秒")
            print(f"   吞吐量: {throughput:.2f} 文件/秒")
            print(f"   传输速度: {speed_mbps:.2f} MB/s")
            return True
        else:
            print("❌ 并发压力测试失败")
            return False
            
    except Exception as e:
        print(f"❌ 并发压力测试异常: {e}")
        return False
    finally:
        # 清理测试文件
        for test_file in test_files:
            if os.path.exists(test_file):
                os.remove(test_file)

def main():
    """主测试函数"""
    print("🚀 开始图片传输优化测试")
    print(f"⚙️  调试模式: {'开启' if DEBUG_MODE else '关闭'}")
    print(f"📊 网络统计: {'开启' if LOG_NETWORK_STATS else '关闭'}")
    
    test_results = []
    
    # 执行所有测试
    tests = [
        ("单张图片传输", test_single_image_transmission),
        ("多张图片并发传输", test_multiple_images_transmission),
        ("大文件传输", test_large_file_transmission),
        ("连接池复用", test_connection_pool_reuse),
        ("重试机制", test_retry_mechanism),
        ("并发压力测试", test_concurrent_stress)
    ]
    
    for test_name, test_func in tests:
        try:
            result = test_func()
            test_results.append((test_name, result))
        except Exception as e:
            print(f"❌ 测试 '{test_name}' 发生异常: {e}")
            test_results.append((test_name, False))
        
        # 测试间隔
        time.sleep(2)
    
    # 输出测试总结
    print("\n" + "=" * 60)
    print("📋 测试总结")
    print("=" * 60)
    
    passed = 0
    for test_name, result in test_results:
        status = "✅ 通过" if result else "❌ 失败"
        print(f"   {test_name}: {status}")
        if result:
            passed += 1
    
    print(f"\n🎯 测试结果: {passed}/{len(test_results)} 通过")
    
    # 最终连接池统计
    final_stats = connection_pool.get_stats()
    print(f"\n🔗 最终连接池统计:")
    print(f"   总创建: {final_stats['created']}")
    print(f"   总复用: {final_stats['reused']}")
    print(f"   总关闭: {final_stats['closed']}")
    print(f"   总错误: {final_stats['errors']}")
    
    # 清理连接池
    connection_pool.close_all()
    print("🧹 连接池已清理")
    
    if passed == len(test_results):
        print("🎉 所有测试通过！图片传输优化成功！")
        return True
    else:
        print("⚠️  部分测试失败，需要进一步优化")
        return False

if __name__ == "__main__":
    try:
        success = main()
        sys.exit(0 if success else 1)
    except KeyboardInterrupt:
        print("\n⏹️  测试被用户中断")
        connection_pool.close_all()
        sys.exit(1)
    except Exception as e:
        print(f"❌ 测试程序异常: {e}")
        connection_pool.close_all()
        sys.exit(1) 