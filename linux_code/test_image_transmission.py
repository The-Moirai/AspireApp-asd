#!/usr/bin/env python3
"""
测试图片传输功能的脚本
用于验证Python虚拟节点与MissionSocketService的图片传输功能
"""
import socket
import cv2
import numpy as np
import os
import sys
import time
import json
import struct
def send_images_to_mission_service(task_id: str, subtask_id: str, image_paths, max_retries: int = 3):
    """
    向 MissionSocketService 发送多张图片
    :param task_id: 任务ID
    :param subtask_id: 子任务ID  
    :param image_paths: 图片文件路径列表
    :param max_retries: 最大重试次数
    """
    for attempt in range(max_retries):
        try:
            # 建立TCP连接
            sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            sock.settimeout(30)  # 设置30秒超时
            sock.connect(("192.168.31.93", 5009))
            
            # 发送图片数据消息头
            message_header = {
                "type": "image_data",
                "content": {
                    "task_id": task_id,
                    "subtask_name": subtask_id,
                    "image_count": len(image_paths)
                }
            }
            
            # 发送消息头
            header_json = json.dumps(message_header)
            sock.sendall(header_json.encode('utf-8'))
            
            # 发送每张图片
            success_count = 0
            for i, image_path in enumerate(image_paths):
                if os.path.exists(image_path):
                    try:
                        # 为每张图片发送单独的头消息
                        send_single_image_with_header(sock, image_path, task_id, subtask_id, i + 1, len(image_paths))
                        success_count += 1
                        print(f"已发送图片 {i+1}/{len(image_paths)}: {image_path}")
                    except Exception as e:
                        print(f"发送图片失败 {image_path}: {e}")
                        break
                else:
                    print(f"图片文件不存在: {image_path}")
            
            if success_count == len(image_paths):
                print(f"成功发送 {success_count} 张图片到 MissionSocketService")
                sock.close()
                return True
            else:
                print(f"部分图片发送失败，成功: {success_count}/{len(image_paths)}")
                
        except Exception as e:
            print(f"发送图片到MissionSocketService失败 (尝试 {attempt + 1}/{max_retries}): {e}")
            if attempt < max_retries - 1:
                print(f"等待 {(attempt + 1) * 2} 秒后重试...")
                time.sleep((attempt + 1) * 2)  # 递增延迟
        finally:
            try:
                sock.close()
            except:
                pass
    
    print(f"发送图片失败，已重试 {max_retries} 次")
    return False

def send_single_image_with_header(sock: socket.socket, image_path: str, task_id: str, subtask_id: str, image_index: int, total_images: int):
    """
    发送带头消息的单张图片文件
    :param sock: TCP socket连接
    :param image_path: 图片文件路径
    :param task_id: 任务ID
    :param subtask_id: 子任务ID
    :param image_index: 图片序号（从1开始）
    :param total_images: 图片总数
    """
    try:
        # 发送图片头消息
        image_header = {
            "type": "single_image",
            "content": {
                "task_id": task_id,
                "subtask_name": subtask_id,
                "image_index": image_index,
                "total_images": total_images,
                "filename": os.path.basename(image_path),
                "filesize": os.path.getsize(image_path)
            }
        }
        
        # 发送JSON头消息
        header_json = json.dumps(image_header)
        sock.sendall(header_json.encode('utf-8'))
        
        # 发送分隔符（用于标识JSON结束）
        sock.sendall(b'\n')
        
        # 直接发送图片文件内容（不包含Python的文件名长度等信息）
        with open(image_path, 'rb') as f:
            while True:
                chunk = f.read(4096)
                if not chunk:
                    break
                sock.sendall(chunk)
        
    except Exception as e:
        print(f"发送带头消息的单张图片失败: {e}")

def send_single_image(sock: socket.socket, image_path: str):
    """
    发送单张图片文件
    :param sock: TCP socket连接
    :param image_path: 图片文件路径
    """
    try:
        # 获取文件信息
        file_name = os.path.basename(image_path)
        file_size = os.path.getsize(image_path)
        
        # 发送文件名长度
        file_name_bytes = file_name.encode('utf-8')
        sock.sendall(struct.pack('I', len(file_name_bytes)))
        
        # 发送文件名
        sock.sendall(file_name_bytes)
        
        # 发送文件大小
        sock.sendall(struct.pack('Q', file_size))
        
        # 发送文件内容
        with open(image_path, 'rb') as f:
            while True:
                chunk = f.read(4096)
                if not chunk:
                    break
                sock.sendall(chunk)
                
    except Exception as e:
        print(f"发送单张图片失败: {e}")

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
        #send_task_completion_info(task_id, subtask_id, "单张图片测试完成")
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
        #send_task_completion_info(task_id, subtask_id, f"多张图片测试完成，共{len(image_paths)}张")
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
        #send_task_completion_info(task_id, subtask_id, f"大图片测试完成，共{len(image_paths)}张")
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
    print(f"目标服务器: {os.environ.get('MISSION_SOCKET_IP', '192.168.31.93')}:{os.environ.get('MISSION_SOCKET_PORT', '5009')}")
    
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
