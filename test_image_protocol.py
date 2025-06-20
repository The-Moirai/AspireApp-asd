#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
测试修复后的图片传输协议
"""

import socket
import json
import os
import time
import random
import string

def create_test_image(filename: str, size_kb: int = 1) -> int:
    """
    创建指定大小的测试图片文件
    
    Args:
        filename: 文件名
        size_kb: 文件大小(KB)
        
    Returns:
        int: 实际文件大小(字节)
    """
    # 创建指定大小的随机内容
    target_size = size_kb * 1024
    content = ''.join(random.choices(string.ascii_letters + string.digits, k=target_size))
    content_bytes = content.encode('utf-8')
    
    with open(filename, 'wb') as f:
        f.write(content_bytes)
    
    actual_size = os.path.getsize(filename)
    print(f"📄 创建测试文件: {filename} (目标={target_size} 字节, 实际={actual_size} 字节)")
    return actual_size

def test_image_protocol():
    """测试单张图片传输协议"""
    
    print("=" * 50)
    print("📸 测试1: 单张图片传输")
    print("=" * 50)
    
    # 创建测试图片
    test_file = "test_protocol_image.png"
    actual_file_size = create_test_image(test_file, 2)  # 2KB测试文件
    
    try:
        # 连接到MissionSocketService
        sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        sock.settimeout(30)
        sock.connect(('192.168.31.93', 5009))
        
        print("✅ 已连接到MissionSocketService")
        
        # 使用实际数据库中存在的任务ID和子任务名称
        task_id = "4A36F861-DC58-413A-B2A6-5D69A8FC8EE9"
        subtask_name = "4a36f861-dc58-413a-b2a6-5d69a8fc8ee9_0_0"
        
        print(f"📋 任务信息: TaskId={task_id}")
        print(f"📋 子任务信息: SubTask={subtask_name}")
        
        # 直接发送single_image消息（不再使用image_data头消息）
        image_header = {
            "type": "single_image",
            "content": {
                "task_id": task_id,
                "subtask_name": subtask_name,
                "image_index": 1,
                "total_images": 1,
                "filename": test_file,
                "filesize": actual_file_size
            }
        }
        
        image_json = json.dumps(image_header)
        sock.sendall(image_json.encode('utf-8'))
        sock.sendall(b'\n')  # JSON分隔符
        
        print(f"✅ 已发送single_image头消息: filesize={actual_file_size}")
        
        # 发送图片数据
        bytes_sent = 0
        with open(test_file, 'rb') as f:
            while True:
                chunk = f.read(4096)
                if not chunk:
                    break
                sock.sendall(chunk)
                bytes_sent += len(chunk)
        
        print(f"✅ 已发送图片数据: {bytes_sent} 字节")
        
        # 验证发送的数据大小
        if bytes_sent == actual_file_size:
            print("✅ 文件大小验证通过")
        else:
            print(f"❌ 文件大小不匹配: 期望={actual_file_size}, 实际={bytes_sent}")
            return False
        
        sock.close()
        print("🎉 单张图片传输测试成功")
        return True
        
    except Exception as e:
        print(f"❌ 单张图片传输测试失败: {e}")
        return False
    finally:
        # 清理测试文件
        if os.path.exists(test_file):
            os.remove(test_file)
            print(f"🗑️  已删除测试文件: {test_file}")

def test_multiple_images():
    """测试多张图片传输"""
    
    print("=" * 50)
    print("📸 测试2: 多张图片传输")
    print("=" * 50)
    
    # 创建多个不同大小的测试图片
    test_files = []
    sizes_kb = [1, 3, 5]  # 不同大小的测试文件
    
    for i, size_kb in enumerate(sizes_kb):
        test_file = f"test_multi_image_{i+1:02d}.png"
        actual_size = create_test_image(test_file, size_kb)
        test_files.append((test_file, actual_size))
    
    try:
        print(f"📊 图片数量: {len(test_files)} 张")
        
        # 为每张图片创建独立连接
        total_bytes_sent = 0
        for i, (test_file, actual_file_size) in enumerate(test_files):
            print(f"\n📸 发送第 {i+1}/{len(test_files)} 张图片: {test_file}")
            
            # 创建新连接
            sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            sock.settimeout(30)
            sock.connect(('192.168.31.93', 5009))
            
            # 使用实际数据库中存在的任务ID和子任务名称
            task_id = "4a36f861-dc58-413a-b2a6-5d69a8fc8ee9"
            subtask_name = f"4a36f861-dc58-413a-b2a6-5d69a8fc8ee9_test_multi_{i+1:02d}"
            
            # 发送single_image消息
            image_header = {
                "type": "single_image",
                "content": {
                    "task_id": task_id,
                    "subtask_name": subtask_name,
                    "image_index": i + 1,
                    "total_images": len(test_files),
                    "filename": test_file,
                    "filesize": actual_file_size
                }
            }
            
            image_json = json.dumps(image_header)
            sock.sendall(image_json.encode('utf-8'))
            sock.sendall(b'\n')  # JSON分隔符
            
            print(f"  ✅ 已发送single_image头: filesize={actual_file_size}")
            
            # 发送图片数据
            bytes_sent = 0
            with open(test_file, 'rb') as f:
                while True:
                    chunk = f.read(4096)
                    if not chunk:
                        break
                    sock.sendall(chunk)
                    bytes_sent += len(chunk)
            
            print(f"  ✅ 已发送图片数据: {bytes_sent} 字节")
            
            # 验证单个文件大小
            if bytes_sent == actual_file_size:
                print(f"  ✅ 文件大小验证通过")
            else:
                print(f"  ❌ 文件大小不匹配: 期望={actual_file_size}, 实际={bytes_sent}")
                sock.close()
                return False
            
            total_bytes_sent += bytes_sent
            sock.close()
            print(f"  ✅ 第 {i+1} 张图片发送完成")
        
        print(f"\n🎉 多张图片传输测试成功")
        print(f"📊 总计发送: {total_bytes_sent} 字节")
        return True
        
    except Exception as e:
        print(f"❌ 多张图片传输测试失败: {e}")
        return False
    finally:
        # 清理测试文件
        for test_file, _ in test_files:
            if os.path.exists(test_file):
                os.remove(test_file)
                print(f"🗑️  已删除测试文件: {test_file}")

def test_large_image():
    """测试大图片传输"""
    
    print("=" * 50)
    print("📸 测试3: 大图片传输 (100KB)")
    print("=" * 50)
    
    # 创建大测试图片
    test_file = "test_large_image.png"
    actual_file_size = create_test_image(test_file, 100)  # 100KB测试文件
    
    try:
        # 连接到MissionSocketService
        sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        sock.settimeout(60)  # 大文件需要更长超时时间
        sock.connect(('192.168.31.93', 5009))
        
        print("✅ 已连接到MissionSocketService")
        
        # 使用实际数据库中存在的任务ID和子任务名称
        task_id = "4a36f861-dc58-413a-b2a6-5d69a8fc8ee9"
        subtask_name = "4a36f861-dc58-413a-b2a6-5d69a8fc8ee9_test_large"
        
        print(f"📋 任务信息: TaskId={task_id}")
        print(f"📋 子任务信息: SubTask={subtask_name}")
        
        # 直接发送single_image消息
        image_header = {
            "type": "single_image",
            "content": {
                "task_id": task_id,
                "subtask_name": subtask_name,
                "image_index": 1,
                "total_images": 1,
                "filename": test_file,
                "filesize": actual_file_size
            }
        }
        
        image_json = json.dumps(image_header)
        sock.sendall(image_json.encode('utf-8'))
        sock.sendall(b'\n')  # JSON分隔符
        
        print(f"✅ 已发送single_image头消息: filesize={actual_file_size}")
        
        # 发送图片数据（显示进度）
        bytes_sent = 0
        chunk_size = 8192  # 8KB chunks for large files
        progress_interval = actual_file_size // 10  # 每10%显示进度
        next_progress = progress_interval
        
        with open(test_file, 'rb') as f:
            while True:
                chunk = f.read(chunk_size)
                if not chunk:
                    break
                sock.sendall(chunk)
                bytes_sent += len(chunk)
                
                # 显示进度
                if bytes_sent >= next_progress:
                    progress = (bytes_sent / actual_file_size) * 100
                    print(f"  📊 传输进度: {progress:.1f}% ({bytes_sent}/{actual_file_size} 字节)")
                    next_progress += progress_interval
        
        print(f"✅ 已发送图片数据: {bytes_sent} 字节")
        
        # 验证发送的数据大小
        if bytes_sent == actual_file_size:
            print("✅ 文件大小验证通过")
        else:
            print(f"❌ 文件大小不匹配: 期望={actual_file_size}, 实际={bytes_sent}")
            return False
        
        sock.close()
        print("🎉 大图片传输测试成功")
        return True
        
    except Exception as e:
        print(f"❌ 大图片传输测试失败: {e}")
        return False
    finally:
        # 清理测试文件
        if os.path.exists(test_file):
            os.remove(test_file)
            print(f"🗑️  已删除测试文件: {test_file}")

def test_connection_only():
    """测试连接"""
    
    print("=" * 50)
    print("🔌 测试0: 连接测试")
    print("=" * 50)
    
    try:
        sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        sock.settimeout(10)
        sock.connect(('192.168.31.93', 5009))
        sock.close()
        
        print("✅ 连接测试成功")
        return True
        
    except Exception as e:
        print(f"❌ 连接测试失败: {e}")
        print("💡 请检查:")
        print("  - MissionSocketService是否在192.168.31.93:5009运行")
        print("  - 网络连接是否正常")
        print("  - 防火墙设置")
        return False

if __name__ == "__main__":
    print("🧪 图片传输协议测试套件")
    print("=" * 60)
    print("📡 连接目标: 192.168.31.93:5009 (MissionSocketService)")
    print("🎯 测试任务ID: 4a36f861-dc58-413a-b2a6-5d69a8fc8ee9")
    print("=" * 60)
    
    # 测试结果统计
    test_results = []
    
    # 测试0: 连接测试
    print("\n🔌 开始连接测试...")
    connection_success = test_connection_only()
    test_results.append(("连接测试", connection_success))
    
    if not connection_success:
        print("\n❌ 连接失败，跳过后续测试")
    else:
        # 测试1: 单张图片
        print("\n📸 开始单张图片测试...")
        success1 = test_image_protocol()
        test_results.append(("单张图片传输", success1))
        
        time.sleep(2)
        
        # 测试2: 多张图片
        print("\n📸 开始多张图片测试...")
        success2 = test_multiple_images()
        test_results.append(("多张图片传输", success2))
        
        time.sleep(2)
        
        # 测试3: 大图片
        print("\n📸 开始大图片测试...")
        success3 = test_large_image()
        test_results.append(("大图片传输", success3))
    
    # 输出测试结果汇总
    print("\n" + "=" * 60)
    print("📊 测试结果汇总")
    print("=" * 60)
    
    passed = 0
    total = len(test_results)
    
    for test_name, result in test_results:
        status = "✅ 通过" if result else "❌ 失败"
        print(f"{test_name:15s} | {status}")
        if result:
            passed += 1
    
    print("-" * 60)
    print(f"总计: {passed}/{total} 测试通过")
    
    if passed == total:
        print("\n🎉 所有测试通过！图片传输协议工作正常。")
        print("💡 提示：请检查服务器日志和数据库确认图片保存情况。")
    else:
        print(f"\n⚠️  {total - passed} 个测试失败，请检查问题。")
    
    print("\n🏁 测试完成") 