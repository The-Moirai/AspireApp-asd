import cv2
import math
import random
import sys
from threading import Thread
import numpy as np
import pickle
import threading
from typing import Dict, List
from concurrent.futures import ThreadPoolExecutor
from cluster import *
import os 
import socket
import struct
import json
import base64
import configparser
import time

# 读取配置文件
def load_config():
    """加载配置文件，如果不存在则创建默认配置"""
    config = configparser.ConfigParser()
    config_file = 'mission_config.ini'
    
    if os.path.exists(config_file):
        config.read(config_file)
    else:
        # 创建默认配置文件
        config['DEFAULT'] = {
            'machine_ip': '192.168.31.35',
            'ui_ip': '192.168.31.192', 
            'alg_ip': '192.168.31.35',
            'ui_port': '5009',
            'mission_socket_ip': '192.168.27.93',
            'mission_socket_port': '5009'
        }
        
        with open(config_file, 'w') as f:
            config.write(f)
        print(f"已创建默认配置文件: {config_file}")
    
    return config

# 加载配置
config = load_config()
machine_ip = config.get('DEFAULT', 'machine_ip')
UI_ip = config.get('DEFAULT', 'ui_ip')
alg_ip = config.get('DEFAULT', 'alg_ip')
UI_port = config.getint('DEFAULT', 'ui_port')

# 图片传输相关配置
MISSION_SOCKET_IP = config.get('DEFAULT', 'mission_socket_ip')
MISSION_SOCKET_PORT = config.getint('DEFAULT', 'mission_socket_port')

print(f"图片传输服务配置: {MISSION_SOCKET_IP}:{MISSION_SOCKET_PORT}")
print("图片传输协议: 使用带头消息的单张图片传输方式")

ans_set:Dict={}#存储每个任务的结果

def send_images_to_mission_service(task_id: str, subtask_id: str, image_paths: List[str], max_retries: int = 3):
    """
    向 MissionSocketService 发送多张图片
    :param task_id: 任务ID
    :param subtask_id: 子任务ID  
    :param image_paths: 图片文件路径列表
    :param max_retries: 最大重试次数
    """
    for attempt in range(max_retries):
        try:
            # 建立TCP连接
            sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            sock.settimeout(30)  # 设置30秒超时
            sock.connect((MISSION_SOCKET_IP, MISSION_SOCKET_PORT))
            
            # 发送图片数据消息头
            message_header = {
                "type": "image_data",
                "content": {
                    "task_id": task_id,
                    "subtask_name": subtask_id,
                    "image_count": len(image_paths)
                }
            }
            
            # 发送消息头
            header_json = json.dumps(message_header)
            sock.sendall(header_json.encode('utf-8'))
            
            # 发送每张图片
            success_count = 0
            for i, image_path in enumerate(image_paths):
                if os.path.exists(image_path):
                    try:
                        # 使用带头消息的方式发送图片
                        send_single_image_with_header(sock, image_path, task_id, subtask_id, i + 1, len(image_paths))
                        success_count += 1
                        print(f"已发送图片 {i+1}/{len(image_paths)}: {image_path}")
                    except Exception as e:
                        print(f"发送图片失败 {image_path}: {e}")
                        break
                else:
                    print(f"图片文件不存在: {image_path}")
            
            if success_count == len(image_paths):
                print(f"成功发送 {success_count} 张图片到 MissionSocketService")
                sock.close()
                return True
            else:
                print(f"部分图片发送失败，成功: {success_count}/{len(image_paths)}")
                
        except Exception as e:
            print(f"发送图片到MissionSocketService失败 (尝试 {attempt + 1}/{max_retries}): {e}")
            if attempt < max_retries - 1:
                print(f"等待 {(attempt + 1) * 2} 秒后重试...")
                time.sleep((attempt + 1) * 2)  # 递增延迟
        finally:
            try:
                sock.close()
            except:
                pass
    
    print(f"发送图片失败，已重试 {max_retries} 次")
    return False

def send_single_image_with_header(sock: socket.socket, image_path: str, task_id: str, subtask_id: str, image_index: int, total_images: int):
    """
    发送带头消息的单张图片文件
    :param sock: TCP socket连接
    :param image_path: 图片文件路径
    :param task_id: 任务ID
    :param subtask_id: 子任务ID
    :param image_index: 图片序号（从1开始）
    :param total_images: 图片总数
    """
    try:
        # 发送图片头消息
        image_header = {
            "type": "single_image",
            "content": {
                "task_id": task_id,
                "subtask_name": subtask_id,
                "image_index": image_index,
                "total_images": total_images,
                "filename": os.path.basename(image_path),
                "filesize": os.path.getsize(image_path)
            }
        }
        
        # 发送JSON头消息
        header_json = json.dumps(image_header)
        sock.sendall(header_json.encode('utf-8'))
        
        # 发送分隔符（用于标识JSON结束）
        sock.sendall(b'\n')
        
        # 直接发送图片文件内容（不包含Python的文件名长度等信息）
        with open(image_path, 'rb') as f:
            while True:
                chunk = f.read(4096)
                if not chunk:
                    break
                sock.sendall(chunk)
        
    except Exception as e:
        print(f"发送带头消息的单张图片失败: {e}")

def send_single_image(sock: socket.socket, image_path: str):
    """
    发送单张图片文件（保留原有接口兼容性）
    :param sock: TCP socket连接
    :param image_path: 图片文件路径
    """
    try:
        # 获取文件信息
        file_name = os.path.basename(image_path)
        file_size = os.path.getsize(image_path)
        
        # 发送文件名长度
        file_name_bytes = file_name.encode('utf-8')
        sock.sendall(struct.pack('I', len(file_name_bytes)))
        
        # 发送文件名
        sock.sendall(file_name_bytes)
        
        # 发送文件大小
        sock.sendall(struct.pack('Q', file_size))
        
        # 发送文件内容
        with open(image_path, 'rb') as f:
            while True:
                chunk = f.read(4096)
                if not chunk:
                    break
                sock.sendall(chunk)
                
    except Exception as e:
        print(f"发送单张图片失败: {e}")

def send_task_completion_info(task_id: str, subtask_id: str, result: str, max_retries: int = 3):
    """
    向 MissionSocketService 发送任务完成信息
    :param task_id: 任务ID
    :param subtask_id: 子任务ID
    :param result: 处理结果描述
    :param max_retries: 最大重试次数
    """
    for attempt in range(max_retries):
        try:
            sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            sock.settimeout(10)  # 设置10秒超时
            sock.connect((MISSION_SOCKET_IP, MISSION_SOCKET_PORT))
            
            # 发送任务结果消息
            message = {
                "type": "task_result",
                "content": {
                    "task_id": task_id,
                    "subtask_name": subtask_id,
                    "result": result
                }
            }
            
            message_json = json.dumps(message)
            sock.sendall(message_json.encode('utf-8'))
            
            print(f"已发送任务完成信息: {subtask_id} - {result}")
            sock.close()
            return True
            
        except Exception as e:
            print(f"发送任务完成信息失败 (尝试 {attempt + 1}/{max_retries}): {e}")
            if attempt < max_retries - 1:
                time.sleep(1)  # 等待1秒后重试
        finally:
            try:
                sock.close()
            except:
                pass
    
    print(f"发送任务完成信息失败，已重试 {max_retries} 次")
    return False

def send_task_info_to_ui(task_info_client, task_info):
    """
    向UI发送任务信息
    :param task_info_client: UI客户端连接
    :param task_info: 任务信息字典
    """
    try:
        if task_info_client is not None:
            task_info_client.sendall(json.dumps(task_info).encode(encoding="utf-8"))
            return True
    except Exception as e:
        print(f"发送任务信息到UI失败: {e}")
    return False

def create_folder_and_save_images_with_transmission(images, folder_name, task_id: str, subtask_id: str):
    """
    创建文件夹保存图片，并传输到MissionSocketService
    :param images: 图片数据列表 [(frame_index, image), ...]
    :param folder_name: 保存文件夹名称
    :param task_id: 任务ID
    :param subtask_id: 子任务ID
    """
    if not os.path.exists(folder_name):
        os.makedirs(folder_name)

    image_paths = []
    
    for frame_index, image in images:
        # 检查 image 是否为 NumPy 数组
        if not isinstance(image, np.ndarray):
            print(f"警告：索引 {frame_index} 的图片不是 NumPy 数组，跳过保存")
            continue
            
        filename = os.path.join(folder_name, f"{frame_index:04d}.png")
        success = cv2.imwrite(filename, image)
        if success:
            image_paths.append(filename)
        else:
            print(f"警告：保存图片失败 {filename}")

    print(f"所有图片已保存至 {folder_name} 文件夹，共 {len(image_paths)} 张")
    
    # 传输图片到MissionSocketService
    if image_paths:
        print(f"开始传输 {len(image_paths)} 张图片到 MissionSocketService...")
        transmission_success = send_images_to_mission_service(task_id, subtask_id, image_paths)
        
        # 发送处理结果信息
        if transmission_success:
            result_info = f"处理完成，生成并成功传输{len(image_paths)}张图片"
            send_task_completion_info(task_id, subtask_id, result_info)
        else:
            result_info = f"处理完成，生成{len(image_paths)}张图片，但传输失败"
            send_task_completion_info(task_id, subtask_id, result_info)
    else:
        print("没有有效的图片需要传输")
        result_info = "处理完成，但没有生成有效图片"
        send_task_completion_info(task_id, subtask_id, result_info)

def create_folder_and_save_images(images, folder_name):
    """创建文件夹并将图片保存进去，按序号命名"""
    if not os.path.exists(folder_name):
        os.makedirs(folder_name)  # 创建文件夹

    for frame_index, image in images:
    
        # 检查 image 是否为 NumPy 数组
        if not isinstance(image, np.ndarray):
            print(f"警告：索引 {frame_index} 的图片不是 NumPy 数组，跳过保存")
            continue
        filename = os.path.join(folder_name, f"{frame_index:04d}.png")  # 按序号命名
        cv2.imwrite(filename, image)  # 保存图片

    print(f"所有图片已保存至 {folder_name} 文件夹")

def generate_random_dag(num_nodes=10, edge_probability=0.3):
    """
    生成一个随机的有向无环图(DAG)的邻接矩阵。
    - num_nodes: 节点数
    - edge_probability: 在 i<j 情况下生成边的概率
    返回值：一个 num_nodes x num_nodes 的二维列表 (邻接矩阵)
    """
    adjacency_matrix = [[0]*num_nodes for _ in range(num_nodes)]
    # 简单保证无环：只允许 i -> j (其中 i < j) 有边
    for i in range(num_nodes):
        for j in range(i+1, num_nodes):
            if random.random() < edge_probability:
                adjacency_matrix[i][j] = 1
    return adjacency_matrix


def split_video_into_segments(video_path, segment_count=100):
    """
    将视频平均分成 segment_count 份（按帧数来均分），返回一个列表 segments，长度为 segment_count。
    每个元素 segments[i] 本质上是若干帧（list of ndarray）。
    若总帧数不是 segment_count 的整数倍，最后一份会稍多一些帧。
    """
    cap = cv2.VideoCapture(video_path)
    if not cap.isOpened():
        print(f"无法打开视频：{video_path}")
        return []

    total_frames = int(cap.get(cv2.CAP_PROP_FRAME_COUNT))
    if total_frames == 0:
        print(f"视频帧数为0，无法切分：{video_path}")
        return []

    # 计算每份大约多少帧
    frames_per_segment = math.floor(total_frames / segment_count)
    if frames_per_segment == 0:
        # 若视频总帧数不足100，则一份至少要1帧，否则就没法继续
        frames_per_segment = 1

    segments = [[] for _ in range(segment_count)]
    current_segment_idx = 0
    frame_idx = 0

    while True:
        ret, frame = cap.read()
        if not ret:
            break  # 视频读完
        segments[current_segment_idx].append((frame_idx,frame))
        frame_idx += 1

        # 判断是否要进入下一个segment
        # （除最后一段外，每段 frames_per_segment 个帧）
        # 最后一段可能包含剩余的所有帧
        if current_segment_idx < segment_count - 1:
            # 如果达到了 frames_per_segment，就切到下一段
            if len(segments[current_segment_idx]) >= frames_per_segment:
                current_segment_idx += 1

    cap.release()

    print(f"总帧数: {total_frames}")
    print(f"分段数: {segment_count}")
    print(f'每份大约 {frames_per_segment} 帧, 最后一份可能稍多。')
    return segments


def group_segments_and_create_DAG(segments, group_count=10):
    """
    将 segments(长度=100) 分为 group_count(=10) 个组，每组 10 份。
    并为每组随机生成一个 10x10 DAG 邻接矩阵。
    返回：
      group_info 列表，长度 = group_count
      其中每个元素形如 {
         'group_index': g,
         'segments': [seg0, seg1, ..., seg9],   # 当前组的10份
         'adjacency_matrix': 10x10的邻接矩阵
      }
    """
    if len(segments) != group_count * 10:
        print("警告：segments 长度不是 group_count*10，检查下逻辑。")
    
    group_info = []
    seg_index = 0
    for g in range(group_count):
        # 取当前组的 10 份
        group_segs = segments[seg_index: seg_index+10]
        seg_index += 10

        # 生成 10x10 的随机DAG邻接矩阵
        dag_matrix = generate_random_dag(num_nodes=10, edge_probability=0.3)

        info = {
            'group_index': g,
            'segments': group_segs,        # 这里是一个包含10个"片段"的列表
            'adjacency_matrix': dag_matrix,
            'size_matrix' : [sys.getsizeof(a[0][1])*len(a)*len(a[0]) for a in group_segs]
        }
        group_info.append(info)

    return group_info




def get_task_groups(video_path):
    segments = split_video_into_segments(video_path, segment_count=100)
    if not segments or len(segments) < 100:
        print("视频分段失败，可能是视频过短或路径错误。")
        return

    # 2) 分为 10 组，每组含 10 份，并生成DAG结构
    group_info_list = group_segments_and_create_DAG(segments, group_count=10)

    # 3) 演示打印结果
    print(f"\n共分成 {len(group_info_list)} 组，每组包含 10 份，共100段。")
    for group_info in group_info_list:
        g_index = group_info['group_index']
        segs = group_info['segments']
        dag = group_info['adjacency_matrix']
        print(f"--- 第 {g_index} 组 ---")
        print(f"  包含片段数: {len(segs)}")
        print(segs[0][0][0])
        # cv2.imshow("Facial Landmarks", segs[0][0])
        # if cv2.waitKey(1) & 0xFF == ord('q'):
        #         break
            
        print(f"size is {group_info['size_matrix']}")
        # print(f"size is {[a.nbytes for a in segs]}")
        print(f"  随机DAG邻接矩阵(10x10):")
        for row in dag:
            print("    ", row)
        print()
    return group_info_list


# get_task_groups("test_objects.mp4")


#################################################以下作为任务管理器使用####################################



class Subtask:
    """
    子任务结构，保存子任务的各种状态信息
    """
    def __init__(self, subtask_id: str, node_ip: str, size: int, content):
        """
        :param subtask_id: 子任务的唯一标识 (如 "task_0", "task_1")
        :param node_ip: 当前处理此子任务的节点 IP
        :param size: 子任务大小(可代表帧数/字节数等)
        :param content: 子任务的内容或描述(可放置帧数据的路径、或者其他描述)
        """
        self.subtask_id = subtask_id
        self.node_ip = node_ip
        self.size = size
        self.content = content
        self.ans=None

        self.finished = False
        self.processing_time = 0.0  # 用于记录该子任务的总处理耗时

    def mark_finished(self):
        self.finished = True

    def set_processing_time(self, t: float):
        self.processing_time = t


class NodeWorker(threading.Thread):
    """
    每个节点一个 worker，串行处理该节点的队列
    """
    def __init__(self,task_name:str,node_ip: str, queue: List["Subtask"],
                 lock: threading.Lock, timeout: float = 10.0):
        super().__init__(daemon=True)
        self.node_ip = node_ip          # 形如 "192.168.1.20:5002"
        self.queue = queue              # 与 TaskManager 共享的列表对象
        self.lock = lock                # 保护 queue
        self.timeout = timeout
        self._stop_event = threading.Event()
        self.task_name=task_name

    def run(self):
        try:
            task_info_client=build_send_client(UI_ip,UI_port)
        except Exception as e:
            task_info_client=None
            print("UI连接错误")
            print(e)
        while not self._stop_event.is_set():
            with self.lock:
                if not self.queue:           # 队列空，退出线程
                    break
                subtask = self.queue.pop(0)  # FIFO

            start_t = time.time()
            try:
                # ---- 建立连接 ----
                ip, port = self._split_ip_port(subtask.node_ip)
                print(f"ip is {ip},port is {port}")
                sock = build_send_client(ip, port)
            except Exception as e:
                try:
                    print(f"{subtask.node_ip}连接出现问题，采用本地负载均衡算法重新分配节点")
                    getNodes=message()
                    getNodes.type="get_nodes_info"
                    getNodes_client=build_send_client(machine_ip,5002)
                    getNodes_data=pickle.dumps(getNodes)
                    send_to_server(getNodes_client,getNodes_data)
                    nodes_data=recv_from_server(getNodes_client)
                    nodes_msg=pickle.loads(nodes_data)
                    nodes=nodes_msg.content
                    msg = message()
                    msg.type = "distribute_algorithm"
                    msg.content = nodes
                    data_to_send = pickle.dumps(msg)
                    client=build_send_client("192.168.27.130",5002)
                    send_to_server(client, data_to_send)

                    # 接收负载均衡模块的响应
                    data = recv_from_server(client)
                    ans = pickle.loads(data)
                    node = ans.content
                    sock = build_send_client(node.ip, node.port)
                    error_info = {
                                    "type": "reassign_info",
                                    "content": {
                                        "old_node_name":       subtask.node_ip,
                                        "subtask_name":    subtask.subtask_id,
                                        "task_name":       self.task_name,
                                        "new_node_name":    node.name
                                    },
                                    "next_node": ""
                                }
                    send_task_info_to_ui(task_info_client, error_info)
                                
                except Exception as e:
                    print(subtask.node_ip+"再分配出现问题")
            try:
                # ---- 序列化消息 ----
                msg = message()
                msg.type = "get_objects_new"
                msg.content = subtask.content
                print("create msg")
                payload = pickle.dumps(msg)
                send_to_server(sock, payload) 
                print("send success")      # 你的 send_to_server 封装
                # sock.shutdown(socket.SHUT_WR)

                # ---- 等待处理结果 ----
                recv_data = recv_from_server(sock) 
                print("receive msg")       # bytes
                ans= pickle.loads(recv_data)  
                subtask.ans =ans.content  # 或自行解析
                subtask.processing_time = time.time() - start_t
                subtask.mark_finished()

                print(f"[{subtask.subtask_id}] 完成，耗时 {subtask.processing_time:.2f}s   task_name is {self.task_name}")
                # print(ans_set[self.task_name])
                ans_set[self.task_name].append(subtask.ans)
                
                # 保存图片并传输到MissionSocketService
                try:
                    create_folder_and_save_images_with_transmission(
                        subtask.ans, 
                        self.task_name, 
                        self.task_name, 
                        subtask.subtask_id
                    )
                    print(f"[{subtask.subtask_id}] 图片保存和传输完成")
                except Exception as e:
                    print(f"[{subtask.subtask_id}] 图片保存和传输失败: {e}")
                if(len(ans_set[self.task_name])==100):
                    abs_path=os.getcwd()
                    task_info = {
                                    "type": "task_info",
                                    "content": {
                                        "node_name":       subtask.node_ip,
                                        "deal_time":       f"{subtask.processing_time:.2f}",
                                        "subtask_name":    subtask.subtask_id,
                                        "task_name":       self.task_name,
                                        "path":abs_path+"\\"+self.task_name
                                    },
                                    "next_node": ""
                                }
                    print(task_info)
                    
                    # 发送主任务完成信息到MissionSocketService
                    try:
                        main_task_result = f"主任务完成，共处理{len(ans_set[self.task_name])}个子任务"
                        success = send_task_completion_info(
                            self.task_name, 
                            "main_task_complete", 
                            main_task_result
                        )
                        if success:
                            print(f"[{self.task_name}] 主任务完成信息发送成功")
                        else:
                            print(f"[{self.task_name}] 主任务完成信息发送失败")
                    except Exception as e:
                        print(f"发送主任务完成信息失败: {e}")
                else:
                    task_info = {
                                    "type": "task_info",
                                    "content": {
                                        "node_name":       subtask.node_ip,
                                        "deal_time":       f"{subtask.processing_time:.2f}",
                                        "subtask_name":    subtask.subtask_id,
                                        "task_name":       self.task_name,
                                        "path":""
                                    },
                                    "next_node": ""
                                }
                
                ####此处需要向前端发送任务处理的情况
                send_task_info_to_ui(task_info_client, task_info)
            except Exception as e:
                print(f"[ERR] {subtask.subtask_id} 发送失败: {e}")
                # 若要重试，可将 subtask 重新放回队列
                with self.lock:
                    self.queue.insert(0, subtask)
                time.sleep(1)        # 简易退避
            finally:
                try:
                    sock.close()
                except Exception:
                    pass

    @staticmethod
    def _split_ip_port(ip_port: str):
        if ":" in ip_port:
            ip, port = ip_port.split(":")
            return ip, int(port)
        # 若没有端口，给默认
        return ip_port, 5002

    def stop(self):
        self._stop_event.set()


class TaskManager:
    """
    维护 {node_ip: [队列]}，并为每个 node_ip 启动一个 NodeWorker
    """
    def __init__(self):
        self.task_name=None
        self.subtasks_map: Dict[str, List[Subtask]] = {}
        self.locks: Dict[str, threading.Lock] = {}
        self.workers: Dict[str, NodeWorker] = {}

    # ---------- 外部接口 ----------
    def add_subtask(self, subtask: Subtask):
        node = subtask.node_ip
        if node not in self.subtasks_map:
            self.subtasks_map[node] = []
            self.locks[node] = threading.Lock()

        with self.locks[node]:
            self.subtasks_map[node].append(subtask)

        # 若该节点的 worker 不存在或已结束，启动一个新的
        if node not in self.workers or not self.workers[node].is_alive():
            worker = NodeWorker(self.task_name,node, self.subtasks_map[node], self.locks[node])
            self.workers[node] = worker
            worker.start()

    # 可选：等待所有节点任务完成
    def wait_all_done(self):
        for w in list(self.workers.values()):
            w.join()

    # 调试输出
    def show_all_tasks(self):
        for node, lst in self.subtasks_map.items():
            for st in lst:
                print(f"{node} -> {st.subtask_id} fin={st.finished} t={st.processing_time:.2f}")




from server_client import *
import time
import json
from control_center_simulator import start_all_vc ,add_one_machine
from control_center import msg_center_server
works:Dict[str, List[TaskManager]] = {}
threads_main=[]
# tasks_group_info=get_task_groups("test_objects.mp4")


# for group_info in tasks_group_info:
#     name=[]
#     g_index ="ptr" + str(group_info['group_index'])
#     t=TaskManager()
#     segs = group_info['segments']
#     size_dag=group_info['size_matrix']
#     for tt in range(len(segs)):
#         t.add_subtask(Subtask(g_index+"_"+str(tt),"?",size_dag[tt],segs[tt]))
#         name.append(g_index+"_"+str(tt))
#     dag = group_info['adjacency_matrix']
    
#     works[g_index]=t
#     ask_client=build_send_client(alg_ip,5008)
#     ask_hr=message()
#     ask_hr.type="ask"
#     ask_hr.content=(name,dag,size_dag)
#     data_to_send=pickle.dumps(ask_hr)
#     send_to_server(ask_client,data_to_send)
def deal_real_worker_message(client,info):
    while True:
        try:
            data = recv_from_server(client)
            print(data)
            text=data.decode()
            msg=json.loads(text)
            print(msg)
            print(f"收到消息类型: {msg['type']} from {info[0]}:{info[1]}")
            msg_type=msg['type']
            if(msg_type=="update_node_info"):
                try:
                    update_msg=message()
                    update_msg.type="update_node_info"
                    update_msg.content=msg['content']
                    update_node_name:str=msg['next_node']
                    t=update_node_name.split(':')
                    update_node_ip=t[0]
                    update_node_port=t[1]
                    update_client=build_send_client(update_node_ip,update_node_port)
                    data_to_send=pickle.dumps(update_msg)
                    send_to_server(update_client,data_to_send)
                    update_client.close()
                    print("已修改节点信息："+update_node_name)
                except:
                    print(update_node_name+"节点信息修改失败")
            elif(msg_type=="add_new_node"):
                new_node_content=msg['content']
                #content需要包含port-->端口号,cpu_memory-->cpu运行内存，memory-->物理存储，bandwidth-->带宽
                add_one_machine(new_node_content["port"],new_node_content["cpu_memory"],new_node_content["bandwidth"],new_node_content["bandwidth"])
                
                
            elif(msg_type=='create_tasks'):
                file=msg['content']
                ptr=msg['next_node']#将任务名存在这里
                ans_set[ptr]=[]
                tasks_group_info=get_task_groups(file)
                
                # tasks_group_info=get_task_groups("test_objects.mp4")
                subtasks_info = {
                                    "type": "Subtasks_info",
                                    "content": {},
                                    "next_node": ""
                                }
                tasks_set={}
                t=TaskManager()
                t.task_name=ptr
                for group_info in tasks_group_info:
                    name=[]
                    g_index =ptr + "_" + str(group_info['group_index'])#每个组的名字为：总任务名+组号

                    segs = group_info['segments']
                    size_dag=group_info['size_matrix']
                    for tt in range(len(segs)):
                        # t.add_subtask(Subtask(g_index+"_"+str(tt),"?",size_dag[tt],segs[tt]))
                        tasks_set[g_index+"_"+str(tt)]=segs[tt]
                        name.append(g_index+"_"+str(tt))
                    dag = group_info['adjacency_matrix']
                    subtasks_info["content"][g_index]=name


                    #此处向负载均衡算法寻找分配方法
                    ask_client=build_send_client(alg_ip,5008)
                    ask_hr=message()
                    ask_hr.type="ask"
                    ask_hr.content=(name,dag,size_dag)
                    data_to_send=pickle.dumps(ask_hr)
                    send_to_server(ask_client,data_to_send)
                #此处接收负载均衡算法
                works[ptr]=t
                dist_data=recv_from_server(ask_client)
                dist_msg:message=pickle.loads(dist_data)
                #此处额外定义一个专门发送任务的函数，采用额外线程，参数有funcs，func由【taskname,ncpname】组成
                funcs=dist_msg.content
                node_task_info:Dict={}
                for func in funcs:
                    task_name=func["task"]

                    task=tasks_set[task_name]
                    ncp_name:str=func["ncp"]
                    ptr=ncp_name.split(":")
                    ncp_ip=ptr[0]
                    ncp_port=int(ptr[1])
                    print(f"task name is {task_name};ncp_name is {ncp_name}; ncp_ip  is {ncp_ip} , ptr is {ptr}")
                    if ncp_name not in node_task_info:

                        node_task_info[ncp_name]=[]
                    node_task_info[ncp_name].append(task_name)
                    
                    t.add_subtask(Subtask(task_name,ncp_name,sys.getsizeof(task),task))
                task_info = {
                                    "type": "tasks_info",
                                    "content": {},
                                    "next_node": ""
                                }
                for key in t.subtasks_map:
                    if key in node_task_info:
                        task_info["content"][key]=[i for i in node_task_info[key]]
                
                ####此处需要向前端发送任务处理的情况
                try:
                    client.sendall(json.dumps(subtasks_info).encode(encoding="utf-8"))
                    print("subtasks_info sended")
                except Exception as e:
                    print(subtasks_info)
                    print(e)
                try:
                    client.sendall(json.dumps(task_info).encode(encoding="utf-8"))
                    print("task_info sended")
                except Exception as e:
                    print(task_info)
                    print(e)    
                thread1=Thread(target=t.wait_all_done)
                threads_main.append(thread1)
                # t.wait_all_done()
                thread1.start()
                
                
                
                    
            elif(msg_type=="start_all"):
                number=msg['content']
                # sum_num=number
                print("start number is "+str(number))
                try:
                    msg_center_server()
                
                    # sum_num+=number
                    start_vc_thread=threading.Thread(target=start_all_vc,args=(number,))
                    # ps=start_all_vc(number)
                    start_vc_thread.start()
                    time.sleep(20)
                    print("11111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111")
                    getNodes=message()
                    getNodes.type="get_nodes_info"
                    getNodes_client=build_send_client(machine_ip,5002)
                    getNodes_data=pickle.dumps(getNodes)
                    send_to_server(getNodes_client,getNodes_data)
                    nodes_data=recv_from_server(getNodes_client)
                    print("2222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222")
                    nodes_msg=pickle.loads(nodes_data)
                    nodes=nodes_msg.content
                    # return_ans:json={}
                    return_ans = {
                                    "type": "start_success",
                                    "content": {
                                        "nodes_name":       [node.name           for node in nodes],
                                        "deal_speed":       [node.deal_speed     for node in nodes],
                                        "radius":           [node.radius         for node in nodes],
                                        "memory":           [node.memory         for node in nodes],
                                        "left_bandwidth":   [node.left_bandwidth for node in nodes],
                                        "x":                [node.x              for node in nodes],
                                        "y":                [node.y              for node in nodes],
                                        "cpu_used_rate":    [node.cpu_used_rate  for node in nodes],
                                    },
                                    "next_node": ""
                                }
                    # msg = message()
                    # msg.type = "start_success"
                    # msg.content = return_ans
                    print("33333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333")
                    print(return_ans)
                    client.sendall(json.dumps(return_ans).encode(encoding="utf-8"))
                    # send_to_server(client,startOk)
                    print("4444444444444444444444444444444444444444444444444444444444444444444444444444444444444444444444444444444444")
                except Exception as e :
                    print("something wrong")
                    print(e)

            elif(msg_type=="node_info"):
                print("node_info")
                getNodes=message()
                getNodes.type="get_nodes_info"
                getNodes_client=build_send_client(machine_ip,5002)
                getNodes_data=pickle.dumps(getNodes)
                send_to_server(getNodes_client,getNodes_data)
                nodes_data=recv_from_server(getNodes_client)
                print("get-ndoes")
                nodes_msg=pickle.loads(nodes_data)
                nodes=nodes_msg.content
                initialize_neighbors(nodes)
                clusters:List=cluster_nodes_by_radius(nodes)#郑奇获取分簇信息
                
                # return_ans:json={}
                return_ans = {
                                "type": "ans_node_info",
                                "content": {
                                    "nodes_name":       [node.name           for node in nodes],
                                    "deal_speed":       [node.deal_speed     for node in nodes],
                                    "radius":           [node.radius         for node in nodes],
                                    "memory":           [node.memory         for node in nodes],
                                    "left_bandwidth":   [node.left_bandwidth for node in nodes],
                                    "x":                [node.x              for node in nodes],
                                    "y":                [node.y              for node in nodes],
                                    "cpu_used_rate":    [node.cpu_used_rate  for node in nodes],
                                    #"cluster":{}
                                },
                                "next_node": ""
                            }
                
                print("get-json")
                print(return_ans)
                cluster_ans={
                                "type":"cluster_info",
                                "content":{},
                                "next_node":""
                                }
                for i in range(len(clusters)):
                   cluster_name = "cluster"+str(i)
                   cluster_ans["content"][cluster_name]=[n.name for n in clusters[i]]
                # send_to_server(client, msg)
                print(cluster_ans)
                # client.sendall(json.dumps(return_ans).encode(encoding="utf-8"))
                client.sendall(json.dumps(return_ans).encode(encoding="utf-8"))
                print("已处理前端node_info 请求，发送结果。")
                client.sendall(json.dumps(cluster_ans).encode(encoding="utf-8"))
                # send_to_server(client,startOk)
                
                print("cluster info send success")
                print("send-to-server")
            elif(msg_type=="shutdown"):
                node_name=msg['content']
                getNodes=message()
                getNodes.type="get_nodes_info"
                getNodes_client=build_send_client(machine_ip,5002)
                getNodes_data=pickle.dumps(getNodes)
                send_to_server(getNodes_client,getNodes_data)
                nodes_data=recv_from_server(getNodes_client)
                print("get-ndoes")
                nodes_msg=pickle.loads(nodes_data)
                nodes=nodes_msg.content
                for node in nodes:
                    if node.name==node_name:
                        shutmsg=message()
                        shutmsg.type="shutdown"
                        shut_client=build_send_client(node.ip,node.port)
                        shutdata=pickle.dumps(shutmsg)
                        send_to_server(shut_client,shutdata)
                        break

                    

        except Exception as e:
            # print(e)
            # break
            pass








# # tasks_group_info=get_task_groups("test_objects.mp4")
# file="test_objects.mp4"
# ptr="test"#将任务名存在这里
# ans_set[ptr]=[]
# tasks_group_info=get_task_groups(file)

# # tasks_group_info=get_task_groups("test_objects.mp4")
# tasks_set={}
# t=TaskManager()
# t.task_name=ptr
# for group_info in tasks_group_info:
#     name=[]
#     g_index ="ptr" + str(group_info['group_index'])
#     segs = group_info['segments']
#     size_dag=group_info['size_matrix']
#     for tt in range(len(segs)):
#         # t.add_subtask(Subtask(g_index+"_"+str(tt),"?",size_dag[tt],segs[tt]))
#         tasks_set[g_index+"_"+str(tt)]=segs[tt]
#         name.append(g_index+"_"+str(tt))
#     dag = group_info['adjacency_matrix']
#     #此处向负载均衡算法寻找分配方法
#     ask_client=build_send_client(alg_ip,5008)
#     ask_hr=message()
#     ask_hr.type="ask"
#     ask_hr.content=(name,dag,size_dag)
#     data_to_send=pickle.dumps(ask_hr)
#     send_to_server(ask_client,data_to_send)
# #此处接收负载均衡算法
# works[g_index]=t
# dist_data=recv_from_server(ask_client)
# dist_msg:message=pickle.loads(dist_data)
# #此处额外定义一个专门发送任务的函数，采用额外线程，参数有funcs，func由【taskname,ncpname】组成
# funcs=dist_msg.content
# node_task_info:Dict={}
# for func in funcs:
#     task_name=func["task"]
#     task=tasks_set[task_name]
#     ncp_name:str=func["ncp"]
#     ptr=ncp_name.split(":")
#     ncp_ip=ptr[0]
#     ncp_port=int(ptr[1])
#     print(f"task name is {task_name};ncp_name is {ncp_name}; ncp_ip  is {ncp_ip} , ptr is {ptr}")
#     if ncp_name not in node_task_info:
#         node_task_info[ncp_name]=[]
#     node_task_info[ncp_name].append(task_name)
    
#     t.add_subtask(Subtask(task_name,ncp_name,sys.getsizeof(task),task))
# task_info = {
#                     "type": "tasks_info",
#                     "content": {},
#                     "next_node": ""
#                 }
# for key in t.subtasks_map:
#     if key in node_task_info:
#         task_info["content"][key]=[i for i in node_task_info[ncp_name]]

#             ####此处需要向前端发送任务处理的情况
# print(task_info)    
# t.wait_all_done()








if __name__ == "__main__":
    work_server=socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    work_server.bind((machine_ip,5007))
    work_server.listen(10)
    while True:
        try:
            client, info = work_server.accept()
            print(f"接受到来自 {info[0]}:{info[1]} 的连接。")
            # client.settimeout(5)
            thread = Thread(target=deal_real_worker_message, args=(client, info))
            threads_main.append(thread)
            print(f"当前线程数: {len(threads_main)}")
            # thread.setDaemon(True)
            thread.start()
        except Exception as e:
            print(f"监听服务器时出错: {e}")











