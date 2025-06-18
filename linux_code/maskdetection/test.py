# #!/usr/bin/env python 
# # -*- coding:utf-8 -*-
# import cv2
# import numpy as np

# detector = cv2.CascadeClassifier('haarcascades\\haarcascade_frontalface_default.xml')
# mask_detector = cv2.CascadeClassifier('xml\\cascade.xml')
# cap = cv2.VideoCapture(0)

# # 同态滤波函数
# def homomorphic_filter(src, d0=10, r1=0.5, rh=2, c=4, h=2.0, l=0.5):
#     gray = src.copy()
#     #if len(src.shape) > 2:
#       #  gray = cv2.cvtColor(src, cv2.COLOR_BGR2GRAY)
#     gray = np.float64(gray)
#     rows, cols = gray.shape

#     gray_fft = np.fft.fft2(gray)
#     gray_fftshift = np.fft.fftshift(gray_fft)
#     dst_fftshift = np.zeros_like(gray_fftshift)
#     M, N = np.meshgrid(np.arange(-cols // 2, cols // 2), np.arange(-rows // 2, rows // 2))
#     D = np.sqrt(M ** 2 + N ** 2)
#     Z = (rh - r1) * (1 - np.exp(-c * (D ** 2 / d0 ** 2))) + r1
#     dst_fftshift = Z * gray_fftshift
#     dst_fftshift = (h - l) * dst_fftshift + l
#     dst_ifftshift = np.fft.ifftshift(dst_fftshift)
#     dst_ifft = np.fft.ifft2(dst_ifftshift)
#     dst = np.real(dst_ifft)
#     dst = np.uint8(np.clip(dst, 0, 255))
#     return dst


# while True:
#     ret, img = cap.read()
#     gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
#     gray = homomorphic_filter(gray)

#     faces = detector.detectMultiScale(gray, 1.1, 3)
#     for (x, y, w, h) in faces:
#         # 参数分别为 图片、左上角坐标，右下角坐标，颜色，厚度
#         face = img[y:y + h, x:x + w]  # 裁剪坐标为[y0:y1, x0:x1]
#         mask_face = mask_detector.detectMultiScale(gray, 1.1, 5)
#         for (x2, y2, w2, h2) in mask_face:
#             cv2.putText(img, 'mask', (x2-2, y2-2),
#                         cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 255, 255))
#             cv2.rectangle(img, (x2, y2), (x2 + w2, y2 + h2), (0, 0, 255), 2)

#     cv2.imshow('Maskdetection', img)
#     cv2.waitKey(3)

# cap.release()
# cv2.destroyAllWindows()

# # 效果一般 而且只能定位有口罩 没有口罩的就不管了







# import torch
# import torch.nn.functional as F
# from torch_geometric.nn import GCNConv
# from torch_geometric.data import Data

# # 1. 构建网络拓扑的数据

# # 节点特征矩阵：每个节点有3个特征，假设是CPU使用率、内存、负载等
# x = torch.tensor([[0.5, 0.3, 0.7],
#                   [0.6, 0.1, 0.5],
#                   [0.4, 0.4, 0.8],
#                   [0.9, 0.2, 0.3],
#                   [0.7, 0.3, 0.9]], dtype=torch.float)

# # 边的连接关系：每两个数字代表一个节点间的边（无向图）
# edge_index = torch.tensor([[0, 1, 1, 2, 3, 4],
#                            [1, 0, 2, 1, 4, 3]], dtype=torch.long)

# # 每个节点的标签：1 表示高负载节点，0 表示低负载节点
# y = torch.tensor([0, 1, 0, 1, 0], dtype=torch.long)

# # 构建图数据
# data = Data(x=x, edge_index=edge_index, y=y)

# # 2. 定义图卷积网络（GCN）模型
# class GCN(torch.nn.Module):
#     def __init__(self, input_dim, hidden_dim, output_dim):
#         super(GCN, self).__init__()
#         self.conv1 = GCNConv(input_dim, hidden_dim)  # 第一层卷积
#         self.conv2 = GCNConv(hidden_dim, output_dim)  # 第二层卷积

#     def forward(self, data):
#         x, edge_index = data.x, data.edge_index
#         x = self.conv1(x, edge_index)  # 进行图卷积
#         x = F.relu(x)  # 激活函数
#         x = self.conv2(x, edge_index)  # 第二次卷积
#         return F.log_softmax(x, dim=1)  # 使用 softmax 进行分类

# # 初始化模型参数
# input_dim = data.num_features  # 输入维度（特征维度）
# hidden_dim = 16  # 隐藏层维度
# output_dim = 2  # 输出维度（高负载或低负载，二分类）

# # 实例化模型
# model = GCN(input_dim, hidden_dim, output_dim)

# # 3. 训练模型
# optimizer = torch.optim.Adam(model.parameters(), lr=0.01, weight_decay=5e-4)  # 优化器
# criterion = torch.nn.CrossEntropyLoss()  # 损失函数

# # 训练函数
# def train():
#     model.train()
#     optimizer.zero_grad()
#     out = model(data)  # 前向传播
#     loss = criterion(out, data.y)  # 计算损失
#     loss.backward()  # 反向传播
#     optimizer.step()  # 更新权重
#     return loss.item()

# # 训练模型
# for epoch in range(200):
#     loss = train()
#     if epoch % 20 == 0:
#         print(f'Epoch {epoch}, Loss: {loss}')

# # 4. 保存训练好的模型
# torch.save(model.state_dict(), 'gcn_model.pth')
# print("Model saved to 'gcn_model.pth'")

# # 5. 模型测试函数，用于评估预测效果
# def test():
#     model.eval()
#     with torch.no_grad():  # 禁用梯度计算
#         pred = model(data).argmax(dim=1)  # 获取预测的类别
#         correct = (pred == data.y).sum()  # 计算预测正确的节点数
#         acc = int(correct) / len(data.y)  # 计算准确率
#         print(f'Accuracy: {acc:.4f}')
#         return pred

# # 使用训练好的模型进行预测
# pred = test()

# # 输出低负载的节点（类别为 0 的节点）
# low_load_nodes = [i for i in range(len(pred)) if pred[i] == 0]
# print(f"Low load nodes: {low_load_nodes}")

# # 6. 在其他节点加载模型并进行推理
# # 加载已保存的模型权重（适用于其他节点或设备上使用）
# model = GCN(input_dim, hidden_dim, output_dim)  # 重新定义模型结构
# model.load_state_dict(torch.load('gcn_model.pth'))  # 加载已保存的模型
# model.eval()  # 切换到评估模式
# print("Model loaded and ready for inference")

# # 加载模型后再次进行预测
# pred = test()
# low_load_nodes = [i for i in range(len(pred)) if pred[i] == 0]
# print(f"Low load nodes after loading the model: {low_load_nodes}")




import numpy as np
import tensorflow as tf

# 数据准备
adj_matrix = np.array([[1, 1, 0, 0, 0],
                       [1, 1, 1, 0, 0],
                       [0, 1, 1, 0, 0],
                       [0, 0, 0, 1, 1],
                       [0, 0, 0, 1, 1]], dtype=np.float32)

features = np.array([[0.5, 0.3, 0.7],
                     [0.6, 0.1, 0.5],
                     [0.4, 0.4, 0.8],
                     [0.9, 0.2, 0.3],
                     [0.7, 0.3, 0.9]], dtype=np.float32)

labels = np.array([0, 1, 0, 1, 0], dtype=np.int32)

num_nodes = adj_matrix.shape[0]

# 自定义图卷积层
class GCNLayer(tf.keras.layers.Layer):
    def __init__(self, output_dim):
        super(GCNLayer, self).__init__()
        self.output_dim = output_dim
    
    def build(self, input_shape):
        # 权重初始化
        self.weight = self.add_weight(shape=(input_shape[-1], self.output_dim),
                                      initializer='glorot_uniform',
                                      trainable=True)
    
    def call(self, features, adj_matrix):
        # 确保支持的计算类型
        support = tf.matmul(features, self.weight)
        out = tf.matmul(adj_matrix, support)
        return out

# 自定义 GCN 模型
class GCNModel(tf.keras.Model):
    def __init__(self, hidden_dim, output_dim):
        super(GCNModel, self).__init__()
        self.gcn1 = GCNLayer(hidden_dim)
        self.gcn2 = GCNLayer(output_dim)
    
    def call(self, features, adj_matrix):
        x = self.gcn1(features, adj_matrix)
        x = tf.nn.relu(x)
        x = self.gcn2(x, adj_matrix)
        return tf.nn.softmax(x, axis=1)

# 初始化模型
hidden_dim = 16
output_dim = 2  # 输出维度（两类：高负载和低负载）
model = GCNModel(hidden_dim, output_dim)

# 定义损失函数和优化器
loss_fn = tf.keras.losses.SparseCategoricalCrossentropy()
optimizer = tf.keras.optimizers.Adam(learning_rate=0.01)

# 训练模型
def train_step(features, adj_matrix, labels):
    with tf.GradientTape() as tape:
        predictions = model(features, adj_matrix)
        loss = loss_fn(labels, predictions)
    
    # 计算梯度并更新权重
    grads = tape.gradient(loss, model.trainable_variables)
    optimizer.apply_gradients(zip(grads, model.trainable_variables))
    
    return loss

# 模型训练
epochs = 200
for epoch in range(epochs):
    loss = train_step(features, adj_matrix, labels)
    if epoch % 20 == 0:
        print(f'Epoch {epoch}, Loss: {loss.numpy():.4f}')

# 保存模型
try:
    model.save('gcn_model')
    print("Model saved successfully.")
except Exception as e:
    print(f"Error saving model: {e}")

# 加载模型并进行推理
try:
    loaded_model = tf.keras.models.load_model('gcn_model', 
        custom_objects={'GCNLayer': GCNLayer, 'GCNModel': GCNModel})

    # 使用加载的模型进行推理
    accuracy, predicted_labels = evaluate(features, adj_matrix, labels)
    print(f'Accuracy after loading model: {accuracy.numpy():.4f}')
    low_load_nodes = [i for i in range(num_nodes) if predicted_labels[i] == 0]
    print(f"Low load nodes after loading model: {low_load_nodes}")
except Exception as e:
    print(f"Error loading model: {e}")
