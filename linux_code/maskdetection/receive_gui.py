from tkinter import *
from tkinter.filedialog import askdirectory
from tkinter.messagebox import showinfo
import cv2
import numpy as np
from PIL import Image, ImageTk
from tkinter import ttk
import pygame
import time

import tensorflow_infer as flow
import socket
import struct
import pickle
import exchange as ex


client_socket=ex.build_recv_server('192.168.137.1', 12345)



# 加载人脸检测器和口罩检测器
detector = cv2.CascadeClassifier('haarcascades\\haarcascade_frontalface_default.xml')
mask_detector = cv2.CascadeClassifier('xml\\cascade.xml')



while 1:
    ans = []
    img=ex.recv_from_server(client_socket)

    # 使用 TensorFlow 进行人脸检测
    num, c, img = flow.inference(img, conf_thresh=0.5, iou_thresh=0.4, target_shape=(260, 260), draw_result=True, show_result=False)
    ans.append(num)
    ans.append(c)
    ans.append(img)
    
    # 序列化处理后的图像数据
    # img_ans = pickle.dumps(ans)
    ex.send_to_server(client_socket,ans)
    print(f'num is {num}, c is {c}')
