#!/usr/bin/env python3
import sys
import os
import cv2
import dlib
import json
from pathlib import Path

# 添加当前目录到Python路径
current_dir = os.path.dirname(os.path.abspath(__file__))
sys.path.append(current_dir)

from get_faces import get_faces

def split_video_into_frames(video_path):
    """将视频分割成帧"""
    cap = cv2.VideoCapture(video_path)
    if not cap.isOpened():
        print(f"Error: 无法打开视频文件 {video_path}")
        return []

    frames = []
    frame_idx = 0
    
    while True:
        ret, frame = cap.read()
        if not ret:
            break
        frames.append((frame_idx, frame))
        frame_idx += 1

    cap.release()
    print(f"提取了 {len(frames)} 帧")
    return frames

def save_processed_frames(processed_frames, output_dir):
    """保存处理后的帧"""
    os.makedirs(output_dir, exist_ok=True)
    
    for frame_index, frame in processed_frames:
        filename = os.path.join(output_dir, f"{frame_index:04d}.png")
        success = cv2.imwrite(filename, frame)
        if not success:
            print(f"警告：无法保存帧 {frame_index}")
    
    print(f"已保存 {len(processed_frames)} 帧到 {output_dir}")

def main():
    if len(sys.argv) != 3:
        print("用法: python process_faces.py <video_path> <output_dir>")
        sys.exit(1)
    
    video_path = sys.argv[1]
    output_dir = sys.argv[2]
    
    try:
        print(f"开始处理视频: {video_path}")
        print(f"输出目录: {output_dir}")
        
        # 检查视频文件是否存在
        if not os.path.exists(video_path):
            print(f"错误：视频文件不存在 {video_path}")
            sys.exit(1)
        
        # 检查人脸模型文件是否存在
        model_path = os.path.join(current_dir, "shape_predictor_68_face_landmarks.dat")
        if not os.path.exists(model_path):
            print(f"错误：人脸模型文件不存在 {model_path}")
            print("请确保 shape_predictor_68_face_landmarks.dat 文件在 linux_code 目录中")
            sys.exit(1)
        
        # 分割视频为帧
        frames = split_video_into_frames(video_path)
        if not frames:
            print("错误：无法从视频中提取帧")
            sys.exit(1)
        
        # 处理人脸识别
        print("开始人脸识别处理...")
        processed_frames = get_faces(frames)
        
        # 保存处理后的帧
        save_processed_frames(processed_frames, output_dir)
        
        # 创建处理结果信息
        result_info = {
            "status": "success",
            "total_frames": len(frames),
            "processed_frames": len(processed_frames),
            "processing_type": "face_detection",
            "output_directory": output_dir
        }
        
        # 保存结果信息
        result_file = os.path.join(output_dir, "processing_info.json")
        with open(result_file, 'w', encoding='utf-8') as f:
            json.dump(result_info, f, ensure_ascii=False, indent=2)
        
        print("人脸识别处理完成！")
        print(f"处理了 {len(processed_frames)} 帧")
        
    except Exception as e:
        print(f"处理过程中发生错误: {e}")
        import traceback
        traceback.print_exc()
        sys.exit(1)

if __name__ == "__main__":
    main() 