#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
测试图片上传到MissionSocketService
"""

import socket
import json
import time
import os

def test_multiple_image_upload():
    print("🧪 开始测试多张图片上传...")
    
    # 目标服务器配置
    host = "192.168.31.93"
    port = 5009
    
    # 使用真实的任务ID和子任务描述
    task_id = "71C9DAA9-14D9-4B90-B125-E28AFC7B75F0"
    subtask_name = "71c9daa9-14d9-4b90-b125-e28afc7b75f0_1_1"
    
    # 模拟多张图片数据
    image_data_list = [
        b'\x89PNG\r\n\x1a\n\x00\x00\x00\rIHDR\x00\x00\x00\x01\x00\x00\x00\x01\x08\x02\x00\x00\x00\x90wS\xde\x00\x00\x00\tpHYs\x00\x00\x0b\x13\x00\x00\x0b\x13\x01\x00\x9a\x9c\x18\x00\x00\x00\nIDATx\x9cc\xf8\x00\x00\x00\x01\x00\x01\x00\x00\x00\x00IEND\xaeB`\x82',
        b'\x89PNG\r\n\x1a\n\x00\x00\x00\rIHDR\x00\x00\x00\x02\x00\x00\x00\x02\x08\x02\x00\x00\x00\x90wS\xde\x00\x00\x00\tpHYs\x00\x00\x0b\x13\x00\x00\x0b\x13\x01\x00\x9a\x9c\x18\x00\x00\x00\nIDATx\x9cc\xf8\x00\x00\x00\x01\x00\x01\x00\x00\x00\x00IEND\xaeB`\x82',
        b'\x89PNG\r\n\x1a\n\x00\x00\x00\rIHDR\x00\x00\x00\x03\x00\x00\x00\x03\x08\x02\x00\x00\x00\x90wS\xde\x00\x00\x00\tpHYs\x00\x00\x0b\x13\x00\x00\x0b\x13\x01\x00\x9a\x9c\x18\x00\x00\x00\nIDATx\x9cc\xf8\x00\x00\x00\x01\x00\x01\x00\x00\x00\x00IEND\xaeB`\x82'
    ]
    
    total_images = len(image_data_list)
    success_count = 0
    
    print(f"📦 准备发送 {total_images} 张图片")
    
    # 为每张图片建立单独的连接（模拟修复后的Linux端行为）
    for i, image_data in enumerate(image_data_list):
        try:
            print(f"\n🔗 为图片 {i+1}/{total_images} 建立新连接...")
            
            # 创建新的socket连接
            with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as sock:
                sock.connect((host, port))
                print(f"✅ 连接成功!")
                
                # 构建JSON消息
                message = {
                    "type": "single_image",
                    "content": {
                        "task_id": task_id,
                        "subtask_name": subtask_name,
                        "filename": f"test_image_{i+1}.png",
                        "filesize": len(image_data),
                        "image_index": i + 1,
                        "total_images": total_images
                    }
                }
                
                print(f"📤 JSON消息: {json.dumps(message, ensure_ascii=False)}")
                print(f"📦 图片数据大小: {len(image_data)} 字节")
                
                # 发送JSON消息
                json_str = json.dumps(message)
                json_bytes = json_str.encode('utf-8')
                sock.sendall(json_bytes + b'\n')  # 添加换行符作为消息分隔符
                print("📤 JSON头消息发送完成")
                
                # 发送图片数据
                sock.sendall(image_data)
                print("📤 图片数据发送完成")
                
                success_count += 1
                print(f"🎉 图片 {i+1} 发送成功!")
                
                # 等待一下让服务器处理
                time.sleep(0.5)
                
        except Exception as e:
            print(f"❌ 图片 {i+1} 发送失败: {e}")
    
    print(f"\n📊 测试结果: 成功发送 {success_count}/{total_images} 张图片")
    
    # 发送任务完成信息
    try:
        print(f"\n🔗 发送任务完成信息...")
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as sock:
            sock.connect((host, port))
            
            completion_message = {
                "type": "task_result",
                "content": {
                    "task_id": task_id,
                    "subtask_name": subtask_name,
                    "result": f"处理完成，成功传输{success_count}张图片"
                }
            }
            
            json_str = json.dumps(completion_message)
            sock.sendall(json_str.encode('utf-8') + b'\n')
            print("✅ 任务完成信息发送成功!")
            
    except Exception as e:
        print(f"❌ 任务完成信息发送失败: {e}")
    
    print("🎉 多张图片上传测试完成!")

if __name__ == "__main__":
    test_multiple_image_upload() 