from tkinter import *
from tkinter.filedialog import askdirectory
from tkinter.messagebox import showinfo
import cv2
import numpy as np
from PIL import Image, ImageTk
from tkinter import ttk
import pygame
import time
import sys
import tensorflow_infer as flow
import socket
import pickle
import struct
server_address = ('192.168.137.1', 12345)

print(socket.gethostbyname(socket.gethostname()))

# 创建套接字对象
server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)

# 绑定地址和端口
server_socket.bind(server_address)

# 监听连接
server_socket.listen(1)
print(f"等待客户端连接在 {server_address} 上...")

# 等待客户端连接
client_socket, client_address = server_socket.accept()
print(f"已连接到客户端 {client_address}")

file_slogan = r'video/slogan.mp3'
file_slogan_short = r'video/slogan_short.mp3'
pygame.mixer.init(frequency=16000, size=-16, channels=2, buffer=4096)

detector = cv2.CascadeClassifier('haarcascades\\haarcascade_frontalface_default.xml')
mask_detector = cv2.CascadeClassifier('xml\\cascade.xml')

class GUI:
	def __init__(self):
		self.camera = None   # 摄像头
		self.root = Tk()
		self.root.title('maskdetection')
		self.root.geometry('%dx%d' % (800, 600))
		self.createFirstPage()
		mainloop()

	def createFirstPage(self):
		self.page1 = Frame(self.root)
		self.page1.pack()
		Label(self.page1, text='欢迎使用口罩检测跟踪系统', font=('粗体', 20)).pack()
		image = Image.open("14.jpg") # 随便使用一张图片做背景界面 不要太大
		photo = ImageTk.PhotoImage(image = image)
		self.data1 = Label(self.page1,  width=780,image = photo)
		self.data1.image = photo
		self.data1.pack(padx=5, pady=5)

		self.button11 = Button(self.page1, width=18, height=2, text="深度学习算法", bg='red', font=("宋", 12),
							   relief='raise',command = self.createSecondPage1)
		self.button11.pack(side=LEFT, padx=25, pady = 10)
		self.button12 = Button(self.page1, width=18, height=2, text="传统算法", bg='green', font=("宋", 12),
		                       relief='raise', command = self.createSecondPage)
		self.button12.pack(side=LEFT, padx=25, pady = 10)
		self.button13 = Button(self.page1, width=18, height=2, text="打开本地视频", bg='white', font=("宋", 12), relief='raise',
							   command = self.select_path)
		self.button13.pack(side=LEFT, padx=25, pady = 10)
		self.button14 = Button(self.page1, width=18, height=2, text="退出系统", bg='gray', font=("宋", 12),
							   relief='raise',command = self.quitMain)
		self.button14.pack(side=LEFT, padx=25, pady = 10)

	def createSecondPage1(self):
		self.camera = cv2.VideoCapture(0)
		self.page1.pack_forget()
		self.page2 = Frame(self.root)
		self.page2.pack()
		Label(self.page2, text='欢迎使用口罩检测跟踪系统', font=('粗体', 20)).pack()
		self.data2 = Label(self.page2)
		self.data2.pack(padx=5, pady=5)

		self.button21 = Button(self.page2, width=18, height=2, text="返回", bg='gray', font=("宋", 12),
							   relief='raise',command = self.backFirst)
		self.button21.pack(padx=25,pady = 10)
		self.video_loop1(self.data2)

	def video_loop1(self, panela):
		
		def slogan_short():

			timeplay = 1.5
			global playflag_short
			playflag_short = 1
			while playflag_short:
				track = pygame.mixer.music.load(file_slogan_short)
				print("------------请您戴好口罩")
				pygame.mixer.music.play()
				time.sleep(timeplay)
				playflag_short = 0
			time.sleep(0)

		success, img = self.camera.read()  # 从摄像头读取照片
		tmpdata=b""
		payload_size=struct.calcsize("I")
		if success:
			print("payload_size is ",payload_size)
			img = cv2.cvtColor(img, cv2.COLOR_BGR2RGB)
			data_to_send=pickle.dumps(img)
			message_size = struct.pack("I", len(data_to_send))
			# msg_size=struct.unpack("I",message_size)[0]
			# print("msg_size is  ",msg_size)
			client_socket.sendall(message_size+data_to_send)
			print("已发送神经网络节点处理")
			print("大小为："+str(sys.getsizeof(data_to_send)))

			while len(tmpdata)<payload_size:
				received_data=client_socket.recv(4)
				if not received_data:
					break
				tmpdata+=received_data
			packed_msg_size=tmpdata[:payload_size]
			tmpdata=tmpdata[payload_size:]
			msg_size=struct.unpack("I",packed_msg_size)[0]
			print("msg_size is ",msg_size)
			while len(tmpdata)<msg_size:
				tmpdata+=client_socket.recv(70000)
			data_ans=tmpdata[:msg_size]
			tmpdata=tmpdata[msg_size:]
			# data_ans = client_socket.recv(1000000)
			ans=pickle.loads(data_ans)
			print("收到处理结果")
			img=ans.pop()
			c=ans.pop()
			num=ans.pop()
			
			# 语音提示
			# if(isinstance(num/5,int)& (c=='NoMask')):
				# slogan_short()

			# cv2.imshow('image', img)
			# img = flow.inference(img, show_result=True, target_shape=(260, 260))
			img = cv2.cvtColor(img, cv2.COLOR_BGR2RGB)

			cv2image = cv2.cvtColor(img, cv2.COLOR_BGR2RGBA)  # 转换颜色从BGR到RGBA
			current_image = Image.fromarray(cv2image)  # 将图像转换成Image对象

			
			imgtk = ImageTk.PhotoImage(image=current_image)
			panela.imgtk = imgtk
			panela.config(image=imgtk)
			self.root.after(1, lambda: self.video_loop1(panela))


	def select_path(self):
		self.pash_= askdirectory()
		path = StringVar()
		path.set(self.pash_)

	def createSecondPage(self):
		self.camera = cv2.VideoCapture(0)
		self.page1.pack_forget()
		self.page2 = Frame(self.root)
		self.page2.pack()
		Label(self.page2, text='欢迎使用口罩检测跟踪系统', font=('粗体', 20)).pack()
		self.data2 = Label(self.page2)
		self.data2.pack(padx=5, pady=5)

		self.button21 = Button(self.page2, width=18, height=2, text="返回", bg='gray', font=("宋", 12),
							   relief='raise',command = self.backFirst)
		self.button21.pack(padx=25,pady = 10)
		self.video_loop(self.data2)

	def video_loop(self, panela):


		success, img = self.camera.read()  # 从摄像头读取照片
		if success:

			faces = detector.detectMultiScale(img, 1.1, 3)
			for (x, y, w, h) in faces:
				# 参数分别为 图片、左上角坐标，右下角坐标，颜色，厚度
				face = img[y:y + h, x:x + w]  # 裁剪坐标为[y0:y1, x0:x1]
				mask_face = mask_detector.detectMultiScale(img, 1.1, 5)
				for (x2, y2, w2, h2) in mask_face:
					cv2.putText(img, 'mask', (x2 - 2, y2 - 2),
								cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 255, 255))
					cv2.rectangle(img, (x2, y2), (x2 + w2, y2 + h2), (0, 0, 255), 2)


			#img = mask.facesdetecter(img)
			cv2image = cv2.cvtColor(img, cv2.COLOR_BGR2RGBA)  # 转换颜色从BGR到RGBA

            #faces = detector.detectMultiScale(cv2image, 1.1, 3)
			current_image = Image.fromarray(cv2image)  # 将图像转换成Image对象
			imgtk = ImageTk.PhotoImage(image=current_image)
			panela.imgtk = imgtk
			panela.config(image=imgtk)
			self.root.after(1, lambda: self.video_loop(panela))




	def backFirst(self):
		self.page2.pack_forget()
		self.page1.pack()
		# 释放摄像头资源
		self.camera.release()
		cv2.destroyAllWindows()

	def backMain(self):
		self.root.geometry('900x600')
		self.page3.pack_forget()
		self.page1.pack()

	def quitMain(self):
		sys.exit(0)





if __name__ == '__main__':

	demo = GUI()


