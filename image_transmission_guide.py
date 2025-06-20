#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
图片传输系统使用指南和示例
"""

import socket
import json
import os
import time
from typing import List

class ImageTransmissionClient:
    """图片传输客户端"""
    
    def __init__(self, server_ip: str = "192.168.31.93", server_port: int = 5009):
        self.server_ip = server_ip
        self.server_port = server_port
        self.timeout = 30
    
    def send_images_to_mission_service(self, task_id: str, subtask_id: str, image_paths: List[str]) -> bool:
        """
        向MissionSocketService发送多张图片
        
        Args:
            task_id: 任务ID (GUID格式)
            subtask_id: 子任务ID
            image_paths: 图片文件路径列表
            
        Returns:
            bool: 是否发送成功
        """
        try:
            # 建立TCP连接
            sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            sock.settimeout(self.timeout)
            sock.connect((self.server_ip, self.server_port))
            
            print(f"✅ 已连接到MissionSocketService: {self.server_ip}:{self.server_port}")
            
            # 1. 发送image_data头消息
            self._send_image_data_header(sock, task_id, subtask_id, len(image_paths))
            
            # 2. 发送每张图片
            success_count = 0
            for i, image_path in enumerate(image_paths):
                if os.path.exists(image_path):
                    try:
                        self._send_single_image(sock, image_path, task_id, subtask_id, i + 1, len(image_paths))
                        success_count += 1
                        print(f"📸 已发送图片 {i+1}/{len(image_paths)}: {os.path.basename(image_path)}")
                    except Exception as e:
                        print(f"❌ 发送图片失败 {image_path}: {e}")
                        break
                else:
                    print(f"⚠️  图片文件不存在: {image_path}")
            
            sock.close()
            
            if success_count == len(image_paths):
                print(f"🎉 成功发送 {success_count} 张图片到 MissionSocketService")
                return True
            else:
                print(f"⚠️  部分图片发送失败，成功: {success_count}/{len(image_paths)}")
                return False
                
        except Exception as e:
            print(f"❌ 连接MissionSocketService失败: {e}")
            return False
    
    def _send_image_data_header(self, sock: socket.socket, task_id: str, subtask_id: str, image_count: int):
        """发送image_data头消息"""
        message_header = {
            "type": "image_data",
            "content": {
                "task_id": task_id,
                "subtask_name": subtask_id,
                "image_count": image_count
            }
        }
        
        header_json = json.dumps(message_header)
        sock.sendall(header_json.encode('utf-8'))
        sock.sendall(b'\n')  # 重要：添加分隔符
        
        print(f"📦 已发送image_data头消息: {image_count} 张图片")
    
    def _send_single_image(self, sock: socket.socket, image_path: str, task_id: str, 
                          subtask_id: str, image_index: int, total_images: int):
        """发送单张图片"""
        # 获取准确的文件大小
        file_size = os.path.getsize(image_path)
        file_name = os.path.basename(image_path)
        
        # 发送single_image头消息
        image_header = {
            "type": "single_image",
            "content": {
                "task_id": task_id,
                "subtask_name": subtask_id,
                "image_index": image_index,
                "total_images": total_images,
                "filename": file_name,
                "filesize": file_size  # 关键：准确的文件大小
            }
        }
        
        # 发送JSON头消息
        header_json = json.dumps(image_header)
        sock.sendall(header_json.encode('utf-8'))
        sock.sendall(b'\n')  # 分隔符
        
        # 发送图片文件内容
        with open(image_path, 'rb') as f:
            bytes_sent = 0
            while True:
                chunk = f.read(4096)
                if not chunk:
                    break
                sock.sendall(chunk)
                bytes_sent += len(chunk)
        
        print(f"  📏 {file_name}: {file_size} 字节, 实际发送: {bytes_sent} 字节")
        
        if bytes_sent != file_size:
            raise Exception(f"文件大小不匹配: 期望{file_size}, 实际发送{bytes_sent}")

def create_sample_images(count: int = 3) -> List[str]:
    """创建示例图片文件"""
    image_paths = []
    
    for i in range(count):
        # 创建不同大小的测试图片内容
        content = f"Sample image {i+1} content for transmission test. " * (10 + i * 5)
        content_bytes = content.encode('utf-8')
        
        filename = f"sample_image_{i+1:03d}.txt"
        
        with open(filename, 'wb') as f:
            f.write(content_bytes)
        
        image_paths.append(filename)
        print(f"📄 创建示例图片: {filename} ({len(content_bytes)} 字节)")
    
    return image_paths

def cleanup_sample_images(image_paths: List[str]):
    """清理示例图片文件"""
    for path in image_paths:
        if os.path.exists(path):
            os.remove(path)
            print(f"🗑️  删除示例文件: {path}")

def demo_image_transmission():
    """演示图片传输功能"""
    print("🚀 图片传输系统演示")
    print("=" * 60)
    
    # 配置参数
    task_id = "4A36F861-DC58-413A-B2A6-5D69A8FC8EE9"  # 实际的任务ID
    subtask_id = "4A36F861-DC58-413A-B2A6-5D69A8FC8EE9_0_1"
    server_ip = "192.168.31.93"
    server_port = 5009
    
    print(f"📋 任务信息:")
    print(f"  - 任务ID: {task_id}")
    print(f"  - 子任务ID: {subtask_id}")
    print(f"  - 服务器: {server_ip}:{server_port}")
    print()
    
    # 创建示例图片
    print("1️⃣ 创建示例图片...")
    image_paths = create_sample_images(3)
    print()
    
    try:
        # 发送图片
        print("2️⃣ 发送图片到MissionSocketService...")
        client = ImageTransmissionClient(server_ip, server_port)
        success = client.send_images_to_mission_service(task_id, subtask_id, image_paths)
        print()
        
        if success:
            print("✅ 图片传输成功完成！")
            print("💡 请检查以下位置确认图片保存:")
            print(f"  - 数据库: SubTaskImages表")
            print(f"  - 文件系统: wwwroot/TaskImages/{task_id}/")
        else:
            print("❌ 图片传输失败！")
            
    finally:
        # 清理示例文件
        print("\n3️⃣ 清理示例文件...")
        cleanup_sample_images(image_paths)
        print()
        print("🏁 演示完成")

if __name__ == "__main__":
    demo_image_transmission() 