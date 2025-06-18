import socket
import pickle
import struct
import sys


def build_recv_server(ip,port):
    server_address = (ip, port)
    print(ip)
    print(server_address)
    print(socket.gethostbyname(socket.gethostname()))

    # 创建套接字对象
    server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)

    # 绑定地址和端口
    server_socket.bind(server_address)
        # 监听连接
    server_socket.listen(1)
    print(f"等待客户端连接...")

    # 等待客户端连接
    client_socket, client_address = server_socket.accept()
    print(f"已连接到客户端 {client_address}")
    return client_socket


def build_send_server(ip,port):
    # 服务器地址和端口
    server_address = (ip, port)

    # 创建套接字对象
    client_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    client_socket.setsockopt(socket.SOL_SOCKET, socket.SO_RCVBUF, 1000000)
    # 连接到服务器
    client_socket.connect(server_address)
    print(f"已连接到服务器 {server_address}")
    return client_socket

def recv_from_server(client_socket):
    payload_size=struct.calcsize("I")
    print("payload_size is ", payload_size)
    data = b""
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
        data += client_socket.recv(7000)
    
    rec_data = data[:msg_size]
    data = data[msg_size:]
    data_all = pickle.loads(rec_data)
    return data_all

    
def send_to_server(client_socket,data):


    data_to_send=pickle.dumps(data)#将要发送的数据进行打包
    message_size = struct.pack("I", len(data_to_send))#生成数据包大小的头文件，告诉服务端接收多少
    # msg_size=struct.unpack("I",message_size)[0]
    # print("msg_size is  ",msg_size)
    client_socket.sendall(message_size+data_to_send)
    print("已发送神经网络节点处理")
    print("大小为："+str(sys.getsizeof(data_to_send)))