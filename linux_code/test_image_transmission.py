#!/usr/bin/env python3
"""
测试图片传输功能的脚本
用于验证Python虚拟节点与MissionSocketService的图片传输功能
"""

import cv2
import numpy as np
import os
import sys
import time
from real_work import send_images_to_mission_service, send_task_completion_info

def create_test_images(count=5, folder_name="test_images"):
    """
    创建测试图片
    :param count: 图片数量
    :param folder_name: 保存文件夹
    :return: 图片路径列表
    """
    if not os.path.exists(folder_name):
        os.makedirs(folder_name)
    
    image_paths = []
    
    for i in range(count):
        # 创建一个彩色测试图片
        height, width = 480, 640
        image = np.zeros((height, width, 3), dtype=np.uint8)
        
        # 添加一些图形和文字
        cv2.rectangle(image, (50, 50), (width-50, height-50), (0, 255, 0), 3)
        cv2.circle(image, (width//2, height//2), 100, (255, 0, 0), -1)
        cv2.putText(image, f"Test Image {i+1}", (width//2-100, height//2), 
                   cv2.FONT_HERSHEY_SIMPLEX, 1, (255, 255, 255), 2)
        cv2.putText(image, f"Timestamp: {int(time.time())}", (50, height-30), 
                   cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255, 255, 0), 1)
        
        # 保存图片
        filename = os.path.join(folder_name, f"test_{i+1:03d}.png")
        cv2.imwrite(filename, image)
        image_paths.append(filename)
        print(f"创建测试图片: {filename}")
    
    return image_paths

def test_single_image_transmission():
    """测试单张图片传输"""
    print("=== 测试单张图片传输 ===")
    
    # 创建单张测试图片
    image_paths = create_test_images(1, "test_single")
    
    # 发送图片
    task_id = "test_task_single"
    subtask_id = "test_subtask_001"
    
    success = send_images_to_mission_service(task_id, subtask_id, image_paths)
    
    if success:
        # 发送完成信息
        send_task_completion_info(task_id, subtask_id, "单张图片测试完成")
        print("✅ 单张图片传输测试成功")
    else:
        print("❌ 单张图片传输测试失败")
    
    return success

def test_multiple_images_transmission():
    """测试多张图片传输"""
    print("\n=== 测试多张图片传输 ===")
    
    # 创建多张测试图片
    image_paths = create_test_images(8, "test_multiple")
    
    # 发送图片
    task_id = "test_task_multiple"
    subtask_id = "test_subtask_002"
    
    success = send_images_to_mission_service(task_id, subtask_id, image_paths)
    
    if success:
        # 发送完成信息
        send_task_completion_info(task_id, subtask_id, f"多张图片测试完成，共{len(image_paths)}张")
        print("✅ 多张图片传输测试成功")
    else:
        print("❌ 多张图片传输测试失败")
    
    return success

def test_large_images_transmission():
    """测试大图片传输"""
    print("\n=== 测试大图片传输 ===")
    
    folder_name = "test_large"
    if not os.path.exists(folder_name):
        os.makedirs(folder_name)
    
    # 创建大尺寸测试图片
    image_paths = []
    for i in range(3):
        height, width = 1080, 1920  # Full HD尺寸
        image = np.random.randint(0, 255, (height, width, 3), dtype=np.uint8)
        
        # 添加标识
        cv2.putText(image, f"Large Test Image {i+1}", (100, 100), 
                   cv2.FONT_HERSHEY_SIMPLEX, 2, (255, 255, 255), 3)
        
        filename = os.path.join(folder_name, f"large_test_{i+1}.png")
        cv2.imwrite(filename, image)
        image_paths.append(filename)
        
        file_size = os.path.getsize(filename) / (1024 * 1024)  # MB
        print(f"创建大图片: {filename} ({file_size:.2f} MB)")
    
    # 发送图片
    task_id = "test_task_large"
    subtask_id = "test_subtask_003"
    
    success = send_images_to_mission_service(task_id, subtask_id, image_paths)
    
    if success:
        send_task_completion_info(task_id, subtask_id, f"大图片测试完成，共{len(image_paths)}张")
        print("✅ 大图片传输测试成功")
    else:
        print("❌ 大图片传输测试失败")
    
    return success

def cleanup_test_files():
    """清理测试文件"""
    test_folders = ["test_images", "test_single", "test_multiple", "test_large"]
    
    for folder in test_folders:
        if os.path.exists(folder):
            import shutil
            shutil.rmtree(folder)
            print(f"清理测试文件夹: {folder}")

def main():
    """主测试函数"""
    print("🚀 开始图片传输功能测试")
    print(f"目标服务器: {os.environ.get('MISSION_SOCKET_IP', '192.168.31.93')}:{os.environ.get('MISSION_SOCKET_PORT', '8080')}")
    
    try:
        # 运行测试
        test_results = []
        
        test_results.append(test_single_image_transmission())
        test_results.append(test_multiple_images_transmission())
        test_results.append(test_large_images_transmission())
        
        # 统计结果
        success_count = sum(test_results)
        total_tests = len(test_results)
        
        print(f"\n📊 测试结果统计:")
        print(f"总测试数: {total_tests}")
        print(f"成功: {success_count}")
        print(f"失败: {total_tests - success_count}")
        print(f"成功率: {success_count/total_tests*100:.1f}%")
        
        if success_count == total_tests:
            print("🎉 所有测试通过！图片传输功能正常")
        else:
            print("⚠️  部分测试失败，请检查网络连接和服务器状态")
        
    except KeyboardInterrupt:
        print("\n⏹️  测试被用户中断")
    except Exception as e:
        print(f"\n❌ 测试过程中发生错误: {e}")
    finally:
        # 清理测试文件
        cleanup_test_files()

if __name__ == "__main__":
    main() 