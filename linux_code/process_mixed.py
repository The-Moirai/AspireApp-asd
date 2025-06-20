#!/usr/bin/env python3
import sys
import os
import cv2
import json
from pathlib import Path

# 添加当前目录到Python路径
current_dir = os.path.dirname(os.path.abspath(__file__))
sys.path.append(current_dir)

from get_faces import get_faces
from get_objects import get_objects

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

def save_processed_frames(processed_frames, output_dir, prefix=""):
    """保存处理后的帧"""
    os.makedirs(output_dir, exist_ok=True)
    
    for frame_index, frame in processed_frames:
        filename = os.path.join(output_dir, f"{prefix}{frame_index:04d}.png")
        success = cv2.imwrite(filename, frame)
        if not success:
            print(f"警告：无法保存帧 {frame_index}")
    
    print(f"已保存 {len(processed_frames)} 帧到 {output_dir}")

def load_yolo_model():
    """加载YOLO模型"""
    try:
        # 尝试导入ultralytics
        from ultralytics import YOLO
        
        # 检查模型文件是否存在
        model_path = os.path.join(current_dir, "yolov8l.pt")
        if not os.path.exists(model_path):
            print(f"错误：YOLO模型文件不存在 {model_path}")
            return None
        
        print("加载YOLO模型...")
        model = YOLO(model_path)
        print("YOLO模型加载成功")
        return model
        
    except ImportError:
        print("错误：未安装ultralytics包")
        print("请运行: pip install ultralytics")
        return None
    except Exception as e:
        print(f"加载YOLO模型时出错: {e}")
        return None

def process_mixed_detection(frames, model):
    """同时进行人脸识别和物体检测"""
    print("开始混合检测处理...")
    
    # 先进行人脸识别
    print("第1步：人脸识别处理...")
    face_frames = get_faces(frames)
    
    # 再进行物体检测（在人脸识别结果基础上）
    print("第2步：物体检测处理...")
    mixed_frames = get_objects(face_frames, model)
    
    return mixed_frames

def main():
    if len(sys.argv) != 3:
        print("用法: python process_mixed.py <video_path> <output_dir>")
        sys.exit(1)
    
    video_path = sys.argv[1]
    output_dir = sys.argv[2]
    
    try:
        print(f"开始混合处理视频: {video_path}")
        print(f"输出目录: {output_dir}")
        
        # 检查视频文件是否存在
        if not os.path.exists(video_path):
            print(f"错误：视频文件不存在 {video_path}")
            sys.exit(1)
        
        # 检查人脸模型文件是否存在
        face_model_path = os.path.join(current_dir, "shape_predictor_68_face_landmarks.dat")
        if not os.path.exists(face_model_path):
            print(f"错误：人脸模型文件不存在 {face_model_path}")
            print("请确保 shape_predictor_68_face_landmarks.dat 文件在 linux_code 目录中")
            sys.exit(1)
        
        # 加载YOLO模型
        yolo_model = load_yolo_model()
        if yolo_model is None:
            print("错误：无法加载YOLO模型")
            sys.exit(1)
        
        # 分割视频为帧
        frames = split_video_into_frames(video_path)
        if not frames:
            print("错误：无法从视频中提取帧")
            sys.exit(1)
        
        # 创建子目录
        faces_dir = os.path.join(output_dir, "faces")
        objects_dir = os.path.join(output_dir, "objects")
        mixed_dir = os.path.join(output_dir, "mixed")
        
        os.makedirs(faces_dir, exist_ok=True)
        os.makedirs(objects_dir, exist_ok=True)
        os.makedirs(mixed_dir, exist_ok=True)
        
        # 处理人脸识别
        print("开始人脸识别处理...")
        face_frames = get_faces(frames)
        save_processed_frames(face_frames, faces_dir, "face_")
        
        # 处理物体检测
        print("开始物体检测处理...")
        object_frames = get_objects(frames, yolo_model)
        save_processed_frames(object_frames, objects_dir, "obj_")
        
        # 混合处理（在原始帧上同时进行两种检测）
        print("开始混合检测处理...")
        mixed_frames = process_mixed_detection(frames, yolo_model)
        save_processed_frames(mixed_frames, mixed_dir, "mixed_")
        
        # 默认使用混合结果作为主输出
        save_processed_frames(mixed_frames, output_dir)
        
        # 创建处理结果信息
        result_info = {
            "status": "success",
            "total_frames": len(frames),
            "face_frames": len(face_frames),
            "object_frames": len(object_frames),
            "mixed_frames": len(mixed_frames),
            "processing_type": "mixed_detection",
            "output_directory": output_dir,
            "subdirectories": {
                "faces": faces_dir,
                "objects": objects_dir,
                "mixed": mixed_dir
            }
        }
        
        # 保存结果信息
        result_file = os.path.join(output_dir, "processing_info.json")
        with open(result_file, 'w', encoding='utf-8') as f:
            json.dump(result_info, f, ensure_ascii=False, indent=2)
        
        print("混合检测处理完成！")
        print(f"人脸识别处理了 {len(face_frames)} 帧")
        print(f"物体检测处理了 {len(object_frames)} 帧")
        print(f"混合检测处理了 {len(mixed_frames)} 帧")
        
    except Exception as e:
        print(f"处理过程中发生错误: {e}")
        import traceback
        traceback.print_exc()
        sys.exit(1)

if __name__ == "__main__":
    main() 