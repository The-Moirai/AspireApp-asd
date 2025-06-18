
import cv2
from PIL import ImageFont, ImageDraw, Image
import numpy as np





def get_objects(image_set,model):
    ans=[]
        # 1. 加载高精度大模型
    print("loading models")
    



    # 3. 推理（设低一点的置信度，更全）
    # print("88888888888888888888888888888888888888888888888888888888888888888888888888")
    # print(len(image_set))
    # print(image_set)
    for index,frame in image_set:


        results = model(frame, conf=0.3)

        # 4. 英文类别名字
        names = model.names

        # 5. 类别英文 → 中文对照表
        name_map = {
            'person': '行人',
            'car': '汽车',
            'bus': '公交车',
            'truck': '卡车',
            'bicycle': '自行车',
            'motorcycle': '摩托车',
            'traffic light': '红绿灯',
            'stop sign': '停车标志'
        }

        # 6. 每种类别对应一种颜色
        color_map = {
            'person': (255, 0, 0),         # 红色
            'car': (0, 0, 255),            # 蓝色
            'bus': (0, 255, 0),            # 绿色
            'truck': (255, 255, 0),        # 青色
            'bicycle': (255, 0, 255),      # 紫色
            'motorcycle': (0, 255, 255),   # 黄色
            'traffic light': (128, 0, 128),# 深紫
            'stop sign': (255, 165, 0)     # 橙色
        }

        # 7. 加载一个支持中文的字体（比如 SimSun）


        # 转成PIL图，用于绘制中文
        img_pil = Image.fromarray(cv2.cvtColor(frame, cv2.COLOR_BGR2RGB))
        draw = ImageDraw.Draw(img_pil)

        # 8. 遍历检测结果
        for result in results:
            boxes = result.boxes
            for box in boxes:
                x1, y1, x2, y2 = map(int, box.xyxy[0])  # 框坐标
                cls_id = int(box.cls[0])                # 类别ID
                conf = box.conf[0]                      # 置信度

                label = names[cls_id]                # 英文类别
                 # 中文，没有就用英文
                color = color_map.get(label, (0, 255, 255))  # 默认黄色

                # 画矩形框
                draw.rectangle([(x1, y1), (x2, y2)], outline=color, width=1)

                # 画标签文字
                text = f"{label} {conf:.2f}"
                
                draw.rectangle([(x1, y1  - 4), (x1  + 4, y1)], fill=color)
                draw.text((x1 + 2, y1 - 2), text, fill=(0, 0, 0))  # 黑色文字

        # 9. 转回OpenCV格式
        annotated_img = cv2.cvtColor(np.array(img_pil), cv2.COLOR_RGB2BGR)

        
        ans.append((index,annotated_img))
    return ans
