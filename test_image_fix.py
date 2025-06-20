#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
测试图片传输修复的脚本
"""

import os
import sys
import time
import cv2
import numpy as np
import socket
import json
import struct

def create_test_image(width=640, height=480, text="Test Image"):
    """创建测试图片"""
    image = np.random.randint(0, 255, (height, width, 3), dtype=np.uint8)
    
    # 添加文本标识
    cv2.putText(image, text, (50, 50), cv2.FONT_HERSHEY_SIMPLEX, 1, (255, 255, 255), 2)
    cv2.putText(image, f"Size: {width}x{height}", (50, 100), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (255, 255, 255), 2)
    cv2.putText(image, f"Time: {time.strftime('%Y-%m-%d %H:%M:%S')}", (50, 150), cv2.FONT_HERSHEY_SIMPLEX, 0.5, (255, 255, 255), 1)
    
    return image

def save_test_image(image, filename):
    """保存测试图片"""
    success = cv2.imwrite(filename, image)
    if success:
        print(f"✅ 测试图片已保存: {filename}")
        return filename
    else:
        print(f"❌ 保存测试图片失败: {filename}")
        return None

def send_single_image_with_header(task_id, subtask_name, image_path, image_index=1, total_images=1, host='localhost', port=5002):
    """
    使用新的JSON头协议发送单张图片
    """
    try:
        # 读取图片文件
        with open(image_path, 'rb') as f:
            image_data = f.read()
        
        filename = os.path.basename(image_path)
        filesize = len(image_data)
        
        print(f"🚀 开始发送图片: {filename} ({filesize} 字节)")
        
        # 创建JSON头消息
        header_message = {
            "type": "single_image",
            "content": {
                "task_id": task_id,
                "subtask_name": subtask_name,
                "filename": filename,
                "filesize": filesize,
                "image_index": image_index,
                "total_images": total_images
            }
        }
        
        # 序列化JSON消息
        json_message = json.dumps(header_message, ensure_ascii=False)
        json_bytes = json_message.encode('utf-8')
        
        # 连接到服务器
        sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        sock.settimeout(30)  # 30秒超时
        sock.connect((host, port))
        
        print(f"📡 已连接到 {host}:{port}")
        
        # 发送JSON头消息 + 换行符分隔符
        sock.sendall(json_bytes + b'\n')
        print(f"📤 JSON头消息已发送: {len(json_bytes)} 字节")
        
        # 发送图片数据
        total_sent = 0
        chunk_size = 4096
        
        while total_sent < filesize:
            chunk = image_data[total_sent:total_sent + chunk_size]
            sent = sock.send(chunk)
            total_sent += sent
            
            # 显示进度
            progress = (total_sent / filesize) * 100
            if total_sent % (64 * 1024) == 0 or total_sent == filesize:  # 每64KB显示一次进度
                print(f"📤 发送进度: {total_sent}/{filesize} ({progress:.1f}%)")
        
        print(f"✅ 图片发送完成: {filename}")
        
        # 关闭连接
        sock.close()
        return True
        
    except Exception as e:
        print(f"❌ 发送图片失败: {e}")
        return False

def send_task_completion_info(task_id, subtask_name, result_info, host='localhost', port=5002):
    """发送任务完成信息"""
    try:
        completion_message = {
            "type": "task_result",
            "content": {
                "task_id": task_id,
                "subtask_name": subtask_name,
                "result": result_info
            }
        }
        
        json_message = json.dumps(completion_message, ensure_ascii=False)
        json_bytes = json_message.encode('utf-8')
        
        sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        sock.settimeout(10)
        sock.connect((host, port))
        
        sock.sendall(json_bytes + b'\n')
        sock.close()
        
        print(f"✅ 任务完成信息已发送: {result_info}")
        return True
        
    except Exception as e:
        print(f"❌ 发送任务完成信息失败: {e}")
        return False

def main():
    """主测试函数"""
    print("🧪 开始图片传输修复测试")
    print("=" * 50)
    
    # 使用日志中的真实任务ID和子任务名称
    task_id = "4a36f861-dc58-413a-b2a6-5d69a8fc8ee9"
    subtask_name = "4a36f861-dc58-413a-b2a6-5d69a8fc8ee9_0_0"  # 使用第一个子任务
    
    print(f"📋 任务ID: {task_id}")
    print(f"📋 子任务名称: {subtask_name}")
    
    # 创建测试文件夹
    test_folder = "test_image_fix"
    if not os.path.exists(test_folder):
        os.makedirs(test_folder)
    
    # 创建并保存测试图片
    test_images = []
    for i in range(3):
        image = create_test_image(640, 480, f"Fix Test {i+1}")
        filename = os.path.join(test_folder, f"fix_test_{i+1}.png")
        saved_path = save_test_image(image, filename)
        if saved_path:
            test_images.append(saved_path)
    
    if not test_images:
        print("❌ 没有成功创建测试图片")
        return
    
    print(f"📸 已创建 {len(test_images)} 张测试图片")
    
    # 发送图片
    success_count = 0
    for i, image_path in enumerate(test_images):
        print(f"\n🚀 发送第 {i+1}/{len(test_images)} 张图片...")
        if send_single_image_with_header(task_id, subtask_name, image_path, i+1, len(test_images)):
            success_count += 1
            time.sleep(1)  # 间隔1秒
    
    # 发送完成信息
    if success_count > 0:
        result_info = f"修复测试完成，成功传输 {success_count}/{len(test_images)} 张图片"
        send_task_completion_info(task_id, subtask_name, result_info)
    
    print("\n" + "=" * 50)
    print(f"🎯 测试完成: {success_count}/{len(test_images)} 张图片传输成功")
    
    if success_count == len(test_images):
        print("✅ 所有图片传输成功！请检查Web界面和数据库是否有图片数据。")
        print("🔍 可以查询数据库: SELECT COUNT(*) FROM SubTaskImages WHERE SubTaskId IN (SELECT Id FROM SubTasks WHERE Description LIKE '%4a36f861%')")
    else:
        print("⚠️  部分图片传输失败，请检查服务器日志。")

if __name__ == "__main__":
    try:
        main()
    except ImportError as e:
        print(f"❌ 缺少依赖库: {e}")
        print("请安装: pip install opencv-python numpy")
    except Exception as e:
        print(f"❌ 运行出错: {e}") 