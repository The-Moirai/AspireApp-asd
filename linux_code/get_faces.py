import cv2
import dlib

# 加载 dlib 的人脸检测器和特征点检测器
face_detector = dlib.get_frontal_face_detector()
landmark_predictor = dlib.shape_predictor("shape_predictor_68_face_landmarks.dat")  # 替换为模型的路径
def test():
    # 创建一个 VideoCapture 对象，读取视频文件
    video_path = "C:/Users/Administrator/Desktop/a.mp4"  # 替换为你的视频文件路径
    cap = cv2.VideoCapture(video_path)

    # 检查视频是否成功打开
    if not cap.isOpened():
        print("Error: 无法打开视频文件")
        exit()

    # 循环读取每一帧
    while True:
        ret, frame = cap.read()
        if not ret:
            print("视频读取完毕或发生错误")
            break

        # 将帧转换为灰度图像
        gray_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)

        # 检测人脸
        faces = face_detector(gray_frame)

        # 遍历检测到的人脸
        for face in faces:
            # 使用特征点检测器检测五官特征点
            landmarks = landmark_predictor(gray_frame, face)

            # 在图像上绘制特征点
            for n in range(68):  # dlib 的 68 个特征点
                x = landmarks.part(n).x
                y = landmarks.part(n).y
                cv2.circle(frame, (x, y), 2, (0, 255, 0), -1)  # 用绿色小圆点标记每个特征点

        # 显示标记后的帧
        cv2.imshow("Facial Landmarks", frame)

        # 按 'q' 键退出
        if cv2.waitKey(1) & 0xFF == ord('q'):
            break

    # 释放视频对象并关闭所有窗口
    cap.release()
    cv2.destroyAllWindows()



def get_faces(image_set):
    ans=[]
    for index,frame in image_set:
        # 将帧转换为灰度图像
        gray_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)

        # 检测人脸
        faces = face_detector(gray_frame)
        for face in faces:
            # 使用特征点检测器检测五官特征点
            landmarks = landmark_predictor(gray_frame, face)

            # 在图像上绘制特征点
            for n in range(68):  # dlib 的 68 个特征点
                x = landmarks.part(n).x
                y = landmarks.part(n).y
                cv2.circle(frame, (x, y), 2, (0, 255, 0), -1)  # 用绿色小圆点标记每个特征点
        ans.append((index,frame))
    return ans








