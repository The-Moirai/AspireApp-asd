import tensorflow_infer as flow
import os
import sys

if hasattr(sys, '_MEIPASS'):  # PyInstaller 打包运行时路径
    cv2_data_path = os.path.join(sys._MEIPASS, 'cv2', 'data')
else:  # 开发环境路径
    import cv2
    cv2_data_path = os.path.join(os.path.dirname(cv2.__file__), 'data')

print(f"OpenCV data path: {cv2_data_path}")
#

def get_face_mask_mode(img):
    num, c, image= flow.inference(img, conf_thresh=0.5, iou_thresh=0.4, target_shape=(260, 260), draw_result=True, show_result=False)
    return image



def deal_image(img,function_name):#输入图像和图像处理的方式，在这里可以进行选择
    if function_name=="get_face_mask":
        image=get_face_mask_mode(img)
        return image