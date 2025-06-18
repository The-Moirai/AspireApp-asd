#!/usr/bin/env python 
# -*- coding:utf-8 -*-
import pandas as pd
import cv2
# 1-300张为正样本 对正样本像素统一
for n in range(1, 300):
    path = 'C:\\Users\\19789\\PycharmProjects\\maskdetection3\\mask\\gray1_2\\' + str(n) + '.jpg'
    # 读取图片
    img = cv2.imread(path)
    img = cv2.resize(img, (50, 50))
    cv2.imwrite('C:\\Users\\19789\\PycharmProjects\\maskdetection3\\mask\\gray1_2\\' + str(n) + '.jpg', img)
    n += 1