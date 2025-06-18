import socket

try:
    import threading
    from PIL import Image
except:
    print("no threading!!!")
import struct
import sys

import select
import errno

PICTURE = 2
PARA = 1
CLIENT_INFO = 0


######本任务中将传输的数据存放在list对象中，在此设定，list[0]--->type，type=0:客户端的本身信息；type=1:传输的参数信息；type=2:传输图片信息.#######
######list[1]--->info,信息的具体内容#######
######list[2]--->next_tag，处理完的信息将转发的下一个节点#######

###这个文件主要做消息通讯的引用库
class message():  # 这里定义消息类，将消息封装成类，以便更好地传参和传输------->需要单独作为一个库
    def __init__(self):
        self.type = None  # 存放任务类型，消息程序根据任务类型进行对应的处理
        # 此处打算设计一下几类消息类型
        # 1，get_node_info--->查询节点的信息
        # 2，ans_node_info--->self.content中将会存放查询到的节点信息
        # 3，get_mask_pic--->content中存放初始图像信息，next_node存储处理节点信息，这里是使用口罩识别，所以是mask，之后可以添加其他类别
        # 4，ans_mask_pic--->content中存放处理后的图像
        # 5，single_node_info--->节点实时更新的数据包信息
        ####注意：这里的设计尽量名称保持一致
        self.content = None  # 根据类型存放内容
        self.next_node = None  # 存放这个消息的目的端


def list_to_binary(data_list):
    binary_data = b""
    for item in data_list:
        if isinstance(item, int):
            # 使用 "Q" 而不是 "I" 以支持更大的整数
            binary_data += struct.pack("B", 1) + struct.pack("Q", item)
        elif isinstance(item, str):
            encoded_str = item.encode('utf-8')
            binary_data += struct.pack("B", 2) + struct.pack("I", len(encoded_str)) + encoded_str
        elif isinstance(item, float):
            binary_data += struct.pack("B", 5) + struct.pack("d", item)  # 5代表float类型
        elif isinstance(item, tuple):
            binary_data += struct.pack("B", 4)  # 4代表tuple类型
            tuple_binary = list_to_binary(list(item))
            binary_data += struct.pack("I", len(tuple_binary)) + tuple_binary
        elif isinstance(item, list):
            sub_binary_data = list_to_binary(item)
            binary_data += struct.pack("B", 3) + struct.pack("I", len(sub_binary_data)) + sub_binary_data
        else:
            raise ValueError(f"Unsupported data type: {type(item)}")
    return binary_data


def binary_to_list(binary_data):
    data_list = []
    index = 0
    while index < len(binary_data):
        item_type = struct.unpack("B", binary_data[index:index + 1])[0]
        index += 1

        if item_type == 1:  # Integer (现在是使用 "Q" 解析)
            item = struct.unpack("Q", binary_data[index:index + 8])[0]
            index += 8
        elif item_type == 2:  # String
            str_len = struct.unpack("I", binary_data[index:index + 4])[0]
            index += 4
            item = binary_data[index:index + str_len].decode('utf-8')
            index += str_len
        elif item_type == 3:  # List
            sub_list_len = struct.unpack("I", binary_data[index:index + 4])[0]
            index += 4
            sub_list_data = binary_data[index:index + sub_list_len]
            item = binary_to_list(sub_list_data)
            index += sub_list_len
        elif item_type == 4:  # Tuple
            tuple_len = struct.unpack("I", binary_data[index:index + 4])[0]
            index += 4
            tuple_data = binary_data[index:index + tuple_len]
            item = binary_to_list(tuple_data)
            index += tuple_len
            item = tuple(item)
        elif item_type == 5:  # Float
            item = struct.unpack("d", binary_data[index:index + 8])[0]
            index += 8
        else:
            raise ValueError(f"Unsupported item type: {item_type}")

        data_list.append(item)

    return data_list


def build_recv_server(ip, port):
    server_address = (ip, port)
    print(ip)
    print(server_address)
    print(socket.gethostbyname(socket.gethostname()))

    # 创建套接字对象
    server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)

    # 绑定地址和端口
    server_socket.bind(server_address)
    # 监听连接
    server_socket.listen(10)
    print(f"等待客户端连接...")

    return server_socket


# def recv_from_server(client_socket):#接收数据
#     payload_size=struct.calcsize("I")
#     # print("payload_size is ", payload_size)
#     data = b""
#     # 接收图像大小信息
#     while len(data) < payload_size:
#         received_data = client_socket.recv(4)
#         if not received_data:
#             break
#         data += received_data

#     packed_msg_size = data[:payload_size]
#     data = data[payload_size:]
#     msg_size = struct.unpack("I", packed_msg_size)[0]
#     print("msg_size is  ", msg_size)

#     # 接收数据
#     while len(data) < msg_size:
#         data += client_socket.recv(128)

#     rec_data = data[:msg_size]
#     data = data[msg_size:]

#     return rec_data


# import struct
# import select
# import socket
# from tqdm import tqdm

# def recv_from_server(client_socket, timeout=10):
#     payload_size = struct.calcsize("I")
#     data = b""

#     # 使用 select 设置超时
#     ready = select.select([client_socket], [], [], timeout)
#     if not ready[0]:
#         print("Timeout: No data received within the specified time.")
#         return -1

#     # 接收图像大小信息
#     while len(data) < payload_size:
#         received_data = client_socket.recv(4)
#         if not received_data:
#             break
#         data += received_data

#     packed_msg_size = data[:payload_size]
#     data = data[payload_size:]
#     msg_size = struct.unpack("I", packed_msg_size)[0]
#     print("msg_size is ", msg_size)

#     total_received = len(data)

#     # 使用 tqdm 显示进度条
#     with tqdm(total=msg_size, unit="B", unit_scale=True, desc="接收数据进度") as progress_bar:
#         # 接收数据
#         while total_received < msg_size:
#             try:
#                 received_data = client_socket.recv(min(4096, msg_size - total_received))
#                 if not received_data:
#                     break
#                 data += received_data
#                 total_received += len(received_data)

#                 # 更新进度条
#                 progress_bar.update(len(received_data))

#             except socket.error as e:
#                 if e.errno != errno.EWOULDBLOCK:
#                     print(f"Socket error: {e}")
#                     break

#     print("数据接收完成！")
#     return data


def recv_from_server(client_socket, timeout=1000):
    payload_size = struct.calcsize("I")
    data = b""

    # 使用 select 设置超时
    ready = select.select([client_socket], [], [], timeout)
    if not ready[0]:
        print("Timeout: No data received within the specified time.")
        return -1

    # 接收图像大小信息
    while len(data) < payload_size:
        received_data = client_socket.recv(4)
        if not received_data:
            break
        data += received_data

    packed_msg_size = data[:payload_size]
    data = data[payload_size:]
    msg_size = struct.unpack("I", packed_msg_size)[0]
    print("msg_size is ", msg_size)

    # 接收数据
    while len(data) < msg_size:
        # try:
            received_data = client_socket.recv(min(409600, msg_size - len(data)))
            # if not received_data:
            #     break
            data += received_data
            print(len(data))
        # except socket.error as e:
        #     # 处理非阻塞模式下的异常
        #     if e.errno != errno.EWOULDBLOCK:
        #         print(f"Socket error: {e}")
        #     break

    return data


def send_to_server(client_socket, data):  # 这里的data应该是二进制数据

    data_to_send = data  # 将要发送的数据进行打包
    message_size = struct.pack("I", len(data_to_send))  # 生成数据包大小的头文件，告诉服务端接收多少
    # msg_size=struct.unpack("I",message_size)[0]
    print("msg_size is  ", len(data_to_send))
    client_socket.sendall(message_size + data_to_send)

    print("已发送神经网络节点处理")
    # print("大小为："+str(sys.getsizeof(data_to_send)))


def handle_client(client_socket):  # 分析解析数据，并实现对应功能
    client_address = client_socket.getpeername()
    try:
        while True:
            data = recv_from_server(client_socket)  # 获取发送来的数据
            data_list = binary_to_list(data)
            data_len = len(data_list)
            if data_len < 3:
                continue
            data_type = data_list.pop()
            data_info = data_list.pop()
            data_dest = data_list.pop()


    except Exception as e:
        print(f"Error handling client {client_address}: {e}")
    finally:
        # 关闭客户端连接
        print(f"Connection from {client_address} closed.")
        client_socket.close()


def start_server():
    # 创建套接字对象
    server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)

    # 绑定地址和端口
    server_address = ('127.0.0.1', 12345)
    server_socket.bind(server_address)

    # 监听连接
    server_socket.listen(5)
    print("Server is listening for incoming connections...")

    try:
        while True:
            # 接受客户端连接
            client_socket, client_address = server_socket.accept()
            # 创建新线程处理客户端连接
            client_handler = threading.Thread(target=handle_client, args=(client_socket,))
            client_handler.start()

    except KeyboardInterrupt:
        print("Server is shutting down...")
    finally:
        # 关闭服务器套接字
        server_socket.close()


def my_info(name, x, y, memory, memory_free):
    seq = []
    seq.append(name)
    seq.append(y)
    seq.append(x)
    seq.append(memory)
    seq.append(memory_free)
    return list_to_binary(seq)


def binary_to_pixel_list(binary_data):
    try:
        # 解析二进制数据中的图像宽度和高度信息
        width, height = struct.unpack("II", binary_data[:8])
        binary_data = binary_data[8:]
        # 初始化像素列表
        pixel_2d_list = []
        # 从二进制数据中逐个读取像素值，并将其添加到像素列表中
        for _ in range(height):
            row = []
            for _ in range(width):
                pixel, = struct.unpack("B", binary_data[:1])
                row.append(pixel)
                binary_data = binary_data[1:]
            pixel_2d_list.append(row)
        return pixel_2d_list
    except Exception as e:
        print(f"Error converting binary to pixel list: {e}")
        return None


def pixel_list_to_binary(pixel_2d_list):
    try:
        binary_data = b""
        # 获取图像的宽度和高度
        width = len(pixel_2d_list[0])
        height = len(pixel_2d_list)
        # 将图像的宽度和高度信息添加到二进制数据中
        binary_data += struct.pack("II", width, height)
        # 将像素数据逐个添加到二进制数据中
        for row in pixel_2d_list:
            for pixel in row:
                # 假设像素值是一个字节大小的整数
                binary_data += struct.pack("B", pixel)
        return binary_data
    except Exception as e:
        print(f"Error converting pixel list to binary: {e}")
        return None


def image_to_2d_list(image, width, height):
    image = image.resize((width, height), Image.Resampling.NEAREST)
    image = image.convert('L')
    image = image.point(lambda x: 0 if x < 128 else 1, '1')

    # 创建一个二维列表
    pixel_data = list(image.getdata())
    pixel_2d_list = []

    for y in range(height):
        row = []
        for x in range(width):
            row.append(pixel_data[y * width + x])
        pixel_2d_list.append(row)

    return pixel_2d_list


def build_send_client(IP, PORT):
    HOST = socket.gethostbyname(IP)
    client = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    client.connect((HOST, PORT))
    return client
# if __name__ == "__main__":
#     start_server()
# total_memory = 10000
# free_memory = 50
# memory=f"total_memory: {total_memory} Bytes"
# memory_free=f"free_memory: {free_memory} Bytes"
# data_to_send=my_info("py32board1",400,500,memory,memory_free)
# print(data_to_send)
# decoded_data = binary_to_list(data_to_send)
# print(decoded_data)
