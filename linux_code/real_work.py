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
machine_ip="192.168.31.35"
UI_ip="192.168.31.93"
alg_ip="192.168.31.35"
UI_port=5009

ans_set:Dict={}#存储每个任务的结果
# def send_image(client_socket, file_name):
#     try:

#         file_size = os.path.getsize(file_name)

#             # 发送文件元数据（文件名长度、文件名、文件大小）
#         file_name_bytes = file_name.encode('utf-8')
#         client_socket.sendall(struct.pack('I', len(file_name_bytes)))  # 文件名长度
#         client_socket.sendall(file_name_bytes)                        # 文件名
#         client_socket.sendall(struct.pack('I', file_size))            # 文件大小

#             # 发送文件内容
#         with open(file_name, 'rb') as f:
#           while chunk := f.read(4096):
#               client_socket.sendall(chunk)


#                #print(f"图片发送完成: {file_name}")
#     except Exception as e:
#         print(f"发送图片时出错: {e}")
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
        if current_segment_idx == segment_count - 1 and len(segments[current_segment_idx]) >= frames_per_segment:
            current_segment_idx=1


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
            'segments': group_segs,        # 这里是一个包含10个“片段”的列表
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
                    client=build_send_client("192.168.31.35",5002)
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
                    if(task_info_client!=None):
                        task_info_client.sendall(json.dumps(error_info).encode(encoding="utf-8"))
                                
                except Exception as e:
                    with self.lock:
                        self.queue.insert(0, subtask)
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
                create_folder_and_save_images(subtask.ans,self.task_name)
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
                if(task_info_client!=None):
                    task_info_client.sendall(json.dumps(task_info).encode(encoding="utf-8"))
            except Exception as e:
                print(f"[ERR] {subtask.subtask_id} 发送失败: {e}")
                # 若要重试，可将 subtask 重新放回队列
                
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
                client.sendall(json.dumps(subtasks_info).encode(encoding="utf-8"))

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
                    try:
                        msg_center_server()
                    except Exception as e:
                        print(e)
                
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

            elif(msg_type=="get_flying"):
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
                    startmsg=message()
                    startmsg.type="get_flying"
                    start_client=build_send_client(node.ip,node.port)
                    startdata=pickle.dumps(startmsg)
                    send_to_server(start_client,startdata)


                    

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











