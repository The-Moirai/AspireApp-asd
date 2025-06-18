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

# 服务器地址和端口
server_address = ('192.168.137.1', 12345)

# 创建套接字对象
client_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
client_socket.setsockopt(socket.SOL_SOCKET, socket.SO_RCVBUF, 1000000)
# 连接到服务器
client_socket.connect(server_address)
print(f"已连接到服务器 {server_address}")

file_slogan = r'video/slogan.mp3'
file_slogan_short = r'video/slogan_short.mp3'
pygame.mixer.init(frequency=16000, size=-16, channels=2, buffer=4096)

# 加载人脸检测器和口罩检测器
detector = cv2.CascadeClassifier('haarcascades\\haarcascade_frontalface_default.xml')
mask_detector = cv2.CascadeClassifier('xml\\cascade.xml')

# 计算图像大小的字节数
payload_size = struct.calcsize("I")
data = b""

while 1:
    ans = []
    print("payload_size is ", payload_size)
    
    # 接收图像大小信息
    while len(data) < payload_size:
        received_data = client_socket.recv(4)
        if not received_data:
            break
        data += received_data
    
    packed_msg_size = data[:payload_size]
    data = data[payload_size:]
    msg_size = struct.unpack("I", packed_msg_size)[0]
    print("msg_size is  ", msg_size)
    
    # 接收图像数据
    while len(data) < msg_size:
        data += client_socket.recv(70000)
    
    img_data = data[:msg_size]
    data = data[msg_size:]
    img = pickle.loads(img_data)

    # 使用 TensorFlow 进行人脸检测
    num, c, img = flow.inference(img, conf_thresh=0.5, iou_thresh=0.4, target_shape=(260, 260), draw_result=True, show_result=False)
    ans.append(num)
    ans.append(c)
    ans.append(img)
    
    # 序列化处理后的图像数据
    img_ans = pickle.dumps(ans)
    message_size = struct.pack("I", len(img_ans))
    
    # 发送处理后的图像数据
    client_socket.sendall(message_size + img_ans)
    print(f'num is {num}, c is {c}')
