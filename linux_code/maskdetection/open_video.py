#!/usr/bin/env python 
# -*- coding:utf-8 -*-
import cv2
# 测试打开摄像头检测跟踪人脸
# 识别人脸的xml文件，构建人脸检测器
detector = cv2.CascadeClassifier('haarcascades\\haarcascade_frontalface_default.xml')
# 获取0号摄像头的实例
cap = cv2.VideoCapture(0)

while True:
    # 就是从摄像头获取到图像，这个函数返回了两个变量，第一个为布尔值表示成功与否，以及第二个是图像。
    ret, img = cap.read()
    #转为灰度图
    gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
    # 获取人脸坐标
    faces = detector.detectMultiScale(gray, 1.1, 3)
    for (x, y, w, h) in faces:
        # 参数分别为 图片、左上角坐标，右下角坐标，颜色，厚度
        cv2.rectangle(img, (x, y), (x + w, y + h), (0, 0, 255), 2)
    cv2.imshow('Mask', img)
    cv2.waitKey(3)

cap.release()
cv2.destroyAllWindows()