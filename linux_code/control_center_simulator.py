
###为了节约资源开支，可以使用选举人算法选举一个临时中心节点，各节点与其通信，实现资源感知，每个节点告知其自身信息，该节点收集所有节点信息并反馈给各节点.
###5002端口用于消息总线的节点收集，5003端口用于图形化展示接收节点信息
#还需要设计一个函数，给消息总线增加一个center_node属性，存储选举产生的中心节点的信息
#一开始所有的节点属于分散状态，彼此之间开始连接，通过比较产生中心节点
#每两个节点连接时，首先获取对方的中心节点信息，再与中心节点连接
#所有节点都会和中心节点连接，当前只是用了一个中心节点，后续可以设计多核心节点
#所有节点会将自身的资源信息实时发送给中心节点汇聚
#当某节点有任务处理时，进入任务分割分配环节，会向中心节点获取所有节点信息
#根据此信息，各节点进行任务分配算法。
from ultralytics import YOLO
model = YOLO('yolov8l.pt')
import os 
import socket, struct, pickle, threading, multiprocessing as mp, time, random, argparse
from dataclasses import dataclass, field
from typing import Dict, Tuple, List
# import subprocess
import pickle
import threading
import time
from server_client import * 
from threading import Thread
from smart_node import *
from get_objects import get_objects
from queue import Queue
import signal
import psutil
process={}
def kill_by_port(port):
    for conn in psutil.net_connections():
        if conn.laddr.port == port and conn.status == 'LISTEN':
            p = psutil.Process(conn.pid)
            p.terminate()

def get_local_ip():
    """
    获取本机的本地IP地址。
    """
    s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    try:
        # 连接到一个不需要响应的地址
        s.connect(('10.255.255.255', 1))
        IP = s.getsockname()[0]
    except Exception as e :
        #print(e)
        IP = '127.0.0.1'
    finally:
        s.close()
    return IP

def get_subnet(ip):
    """
    获取给定IP地址的子网地址，假设子网掩码为255.255.255.0。
    """
    return '.'.join(ip.split('.')[:3]) + '.'

# def is_host_active(ip):
#     """
#     检查指定IP是否处于活动状态。
#     """
#     try:
#         # 使用ping命令检查IP是否活动
#         output = subprocess.check_output(['ping', '-c', '1', '-W', '1', ip],
#                                          stderr=subprocess.STDOUT, universal_newlines=True)
#         if '1 received' in output or 'bytes from' in output:
#             return True
#     except subprocess.CalledProcessError:
#         return False
#     return False

class ControlCenter:
    def __init__(self, idx: int, port: int):
        self.threads = []
        self.host=get_local_ip()
        self.port=port+idx
        #print(f"socket.gethostbyname(socket.gethostname()) is {self.host}")
        self.monitor_addr = (self.host, self.port)
        self.monitor_server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.monitor_server.bind(self.monitor_addr)
        self.monitor_server.listen(110)  # 创建接收信息的服务
        self.nodes: list[real_node] = []
        # self.dealing_tasks=Queue(maxsize=3)
        # self.waiting_tasks=Queue(maxsize=-1)
        self.connection_pool = {}
        self.lock = threading.Lock()  # 线程锁以确保线程安全

        # 初始化自身节点信息
        self.self_node = get_my_node(self.port)
        self.self_node.port=port+idx

        self.center_node: real_node = self.self_node
        self.nodes.append(self.self_node)
        #print(f"自身节点信息已初始化: {self.self_node.name}")
        self.stop_tag=False
        self.t1=Thread(target=self.get_monitor_server, daemon=True)
        self.t1.start()
        self.t2=Thread(target=self.scan_and_elect, daemon=True)
        self.t2.start()
        self.t3=Thread(target=self.send_my_node_info, daemon=True)

        self.t3.start()
        self.t4=Thread(target=self.monitor_real_node,daemon=True)
        self.t4.start()



    def monitor_connection(self, client_socket):
        try:
            # 发送一个空的心跳包以检测连接是否仍然存活
            client_socket.send(b'')
            return True
        except (socket.error, socket.timeout):
            return False

    def get_single_node_info(self, smnode):
        hostname = smnode.name
        with self.lock:
            all_nodes_name = [node.name for node in self.nodes]
            if smnode.name not in all_nodes_name:
                self.nodes.append(smnode)
                # if(self.self_node.name==self.center_node.name):
                #     self.tell_nodes_net()
                #print(f"新节点已加入: {smnode.name}")
            else:
                for node_old in self.nodes:
                    if node_old.name == smnode.name:
                        # 更新节点信息
                        node_old.position = smnode.position
                        node_old.memory = smnode.memory
                        node_old.deal_speed = smnode.deal_speed
                        node_old.signal = smnode.signal
                        node_old.receive_tag = smnode.receive_tag
                        node_old.cpu_memory = smnode.cpu_memory
                        node_old.initial_compute_capacity = smnode.initial_compute_capacity
                        node_old.dealed_task_num = smnode.dealed_task_num
                        node_old.algorithm_type = smnode.algorithm_type
                        node_old.cpu_used_memory = smnode.cpu_used_memory
                        node_old.cpu_used_rate = smnode.cpu_used_rate
                        node_old.bandwidth = smnode.bandwidth
                        node_old.time=smnode.time
                        node_old.dealing_task_num=smnode.dealing_task_num
                        node_old.x=smnode.x
                        node_old.y=smnode.y
                        node_old.waiting_task_num=smnode.waiting_task_num
                        # node_old.neighbors=smnode.neighbors
                        break
                #print(f"节点信息已更新: {smnode.name}")

    def ans_node_info(self, client, info):
        """
        回复获取节点信息的请求，发送自身节点的信息。
        """
        msg_ans = message()
        msg_ans.content = self.center_node if self.center_node else self.self_node
        msg_ans.type = "ans_node_info"

        data_to_send = pickle.dumps(msg_ans)
        send_to_server(client, data_to_send)
        #print(f"已回复节点信息请求给 {info[0]}:{info[1]}")
    

    
    def ans_nodes_info(self,client,info):#用于回复get_nodes_info的请求，发送所有本机知道的节点信息
        
        #print("======================0000============================")
        msg_ans=message()
        #print("======================1111============================")
        # node_all=pickle.dumps(self.nodes)
        msg_ans.content=self.nodes
        #print("======================2222============================")
        # msg_ans.next_node=info
        #print("======================3333============================")
        msg_ans.type="ans_node_info"

        data_to_send=pickle.dumps(msg_ans)
        send_to_server(client,data_to_send)
    def get_nodes_info(self,node_info):#存储获取到的节点信息
        self.nodes=None
        self.nodes=node_info
    def elect_center_node(self):
        """
        选举中心节点，基于节点名称的字典序，名称最大的节点为中心节点。
        """
        with self.lock:
            if not self.nodes:
                #print("没有节点可供选举中心节点。")
                return
            # 假设节点名称唯一且可用于比较
            self.nodes.sort(key=lambda x: x.cpu_memory, reverse=True)
            self.center_node = self.nodes[0]
            #print(f"中心节点已选举: {self.center_node.name}")
        

    def scan_network(self):
        """
        扫描本地网络，发现其他节点并获取其信息。
        测试时，直接先测试主机，提高连接速度
        """
        local_ip = self.self_node.ip
        # local_ip = "192.168.209.247"
        
        subnet = get_subnet(local_ip)
        #print(f"开始扫描子网: {subnet}0/24")
        for i in range(34, 36):
            ip = f"{subnet}{i}"
            # if ip == local_ip:
            #     continue
            if ip:
            
                try:
                    sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
                    sock.settimeout(0.5)
                    sock.connect((ip, 5002))
                    #print(f"发现活动主机: {ip}")
                    #print(f"已连接到 {ip}:5002")
                    # 发送获取节点信息的请求
                    msg = message()
                    msg.type = "get_node_info"
                    data_to_send = pickle.dumps(msg)
                    send_to_server(sock, data_to_send)
                    #print(f"已发送获取节点信息请求给 {ip}:5002")

                    # 接收回复
                    data = recv_from_server(sock)
                    if data:
                        msg_ans = pickle.loads(data)
                        if msg_ans.type == "ans_node_info":
                            smnode = msg_ans.content
                            # 检查扫描节点是否有center_node
                            if smnode:
                                # 连接到扫描节点的center_node进行比较
                                # center_name = smnode.center_node.ip
                                center_name = smnode.name
                                #print(f"{ip} 已有中心节点: {center_name} ({center_name})")
                                try:
                                    center_sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
                                    center_sock.settimeout(1)
                                    center_sock.connect((smnode.ip, 5002))
                                    #print(f"已连接到 {center_name}以获取中心节点信息")
                                    # 发送获取中心节点信息的请求
                                    center_msg = message()
                                    center_msg.type = "get_node_info"
                                    center_data = pickle.dumps(center_msg)
                                    send_to_server(center_sock, center_data)
                                    #print(f"已发送获取中心节点信息请求给 {center_name}")

                                    # 接收中心节点信息
                                    center_data_received = recv_from_server(center_sock)
                                    if center_data_received:
                                        center_msg_ans = pickle.loads(center_data_received)
                                        if center_msg_ans.type == "ans_node_info":
                                            center_node_info = center_msg_ans.content
                                            self.get_single_node_info(center_node_info)
                                            #print(f"已接收中心节点信息: {center_node_info.name}")
                                            self.center_node=center_node_info
                                    center_sock.close()
                                except Exception as e:
                                    print(f"无法连接到扫描节点的中心节点 {center_name}:5002 - {e}")
                            else:
                                # 如果扫描节点没有center_node，则将其信息加入
                                self.get_single_node_info(smnode)
                                #print(f"已接收来自 {ip} 的节点信息: {smnode.name}")
                    sock.close()
                    break
                except Exception as e:
                    print(f"无法连接到 {ip}:5002 - {e}")

        # 完成扫描后进行中心节点选举
        # self.elect_center_node()
        self.tell_all_center_node()
    def cleanup_connection_pool(self):
    # 收集所有无效的 IP
        try:
            ips_to_remove = [ip for ip, client in self.connection_pool.items() if not is_client_alive(client)]
            for ip in ips_to_remove:
                client = self.connection_pool.pop(ip)
                client.close()  # 关闭连接
                #print(f"已删除无效连接: {ip}")
        except:
            pass
        # 删除无效的客户端连接
    def connect_to_center_node(self):
        """
        连接到中心节点并发送自身信息。
        """
        if not self.center_node:
            #print("尚未选举出中心节点，无法连接。")
            return

        if self.center_node.name == self.self_node.name:
            #print("当前节点已是中心节点。")
            return

        try:
            sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            sock.connect((self.center_node.ip, self.center_node.port))
            #print(f"已连接到中心节点 {self.center_node.ip} at {self.center_node.port}:5002")
            # 发送自身节点信息
            msg = message()
            msg.type = "single_node_info"
            msg.content = self.self_node
            data_to_send = pickle.dumps(msg)
            send_to_server(sock, data_to_send)
            #print(f"已发送自身节点信息给中心节点 {self.center_node.name}")
            sock.close()
        except Exception as e:
            print(f"无法连接到中心节点 {self.center_node.name} at {self.center_node.name}:5002 - {e}")

    # def send_my_node_info(self):
    #     """
    #     定期向中心节点发送自身节点信息。
    #     """
    #     last_node_name=self.center_node.name
    #     while True:
    #         if last_node_name != self.center_node.name:
    #             last_node_name=self.center_node.name
    #             sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    #             sock.settimeout(5)
    #             sock.connect((self.center_node.ip, 5002))
    #             #print(f"连接到中心节点 {self.center_node.name} at {self.center_node.ip}:5002 发送自身信息。")
    #         try:
                
    #             msg = message()
    #             msg.type = "single_node_info"
    #             msg.content = self.self_node
    #             data_to_send = pickle.dumps(msg)
    #             send_to_server(sock, data_to_send)
    #             sock.close()
    #             #print(f"已发送自身节点信息给中心节点 {self.center_node.name}")
    #         except Exception as e:
    #             #print(f"发送自身信息到中心节点失败: {e}")
    #         time.sleep(1)
    def tell_nodes_net(self):
        msg=message()
        msg.type="ans_nodes_info"
        msg.content=self.nodes
        data_to_send = pickle.dumps(msg)
        clients=[]
        for node in self.nodes:
            if(node.name==self.self_node.name):
                continue
            client=build_send_client(node.ip,node.port)
            send_to_server(client,data_to_send)
            clients.append(client)
    def send_my_node_info(self):
        """
        定期向中心节点发送自身节点信息。
        """
        PORT=self.center_node.port
        last_node_name=self.center_node.name
        HOST=socket.gethostbyname(self.center_node.ip)
        #print(f"host is {HOST},port is {PORT}")
        client=socket.socket(socket.AF_INET,socket.SOCK_STREAM)
        client.connect((HOST,PORT))
        connection_pool[last_node_name]=client
        # node=self.self_node
        self.self_node.refresh_info()
        while True:
            try:
                if(self.stop_tag):
                    client.close()
                    self.cleanup_connection_pool() 
                    break
            # 在发送信息前检查连接是否有效
                if not self.monitor_connection(client):
                    #print("检测到中心节点连接断开，重新选举中心节点...")
                    client.close()
                    self.cleanup_connection_pool()  # 清理连接池
                    for node_old in self.nodes:
                        if node_old.name == self.center_node.name:
                                    
                            self.nodes.remove(node_old)
                    # 重新选举中心节点并连接
                    # self.elect_center_node()
                    self.tell_all_center_node()
                    # 重新初始化连接
                    client = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
                    client.connect((self.center_node.ip, self.center_node.port))
                    connection_pool[self.center_node.name] = client
                # ... 发送节点信息 ...
            except Exception as e:
                pass
                #print(f"发送信息失败: {e}")
            #     time.sleep(5)  # 重试前等待
            #print(f"last_node_name is {last_node_name} and center_node.ip is {self.center_node.name}")
            if last_node_name != self.center_node.name: 
                try:
                    self.tell_all_center_node()
                except:         
                    #print("tell process something wrong")
                    pass
                client.close()
                self.cleanup_connection_pool()
                last_node_name=self.center_node.name
                # HOST=socket.gethostbyname("192.168.137.1")
                HOST=socket.gethostbyname(self.center_node.ip)
                PORT=self.center_node.port
                client=socket.socket(socket.AF_INET,socket.SOCK_STREAM)
                #print(f"host is {HOST},port is {PORT}")
                client.connect((HOST,PORT))
                #print("connect success")
                connection_pool[last_node_name]=client
                

            self.self_node.refresh_info()
            self.cleanup_connection_pool()
            send_my_info(client,self.self_node)
            time.sleep(2)
    def ans_face_mask(self, client, img):
        self.self_node.dealing_task_num+=1
        from cv_mode import deal_image
        # 这里放入调用图像识别的函数, image为处理后的结果图
        image = deal_image(img, "get_face_mask")
        ans_msg = message()
        ans_msg.type = "ans_face_mask"
        ans_msg.content = image
        data_to_send = pickle.dumps(ans_msg)
        send_to_server(client, data_to_send)
        self.self_node.dealed_task_num+=1
        self.self_node.dealing_task_num-=1
        


    def get_objects(self,frame):
        
        global dealed_tasks
        dealed_tasks+=1
        return get_objects(frame,model)
    def get_faces(self, frame):
        # 这里传入的是图片集，格式为(index, frame)序号+图
        from get_faces import get_faces
        global dealed_tasks
        dealed_tasks+=1
        return get_faces(frame)
    def get_objects_new(self,frame):
        global dealed_tasks
        dealed_tasks+=1
        return get_objects(frame,model)
    def deal_message(self, client, info):
        heart_beat = 0  # 判断是否是心跳的，各节点同步信息的连接叫做心跳
        ### 这一块负责消息处理
    
        while True:
            if self.stop_tag==True:
                client.close()
                break
            try:
                #print("center node is "+self.center_node.name)
                if heart_beat:
                    data = recv_from_server(client,timeout=10)  # 该线程循环接收数据
                else:
                    data=recv_from_server(client)
                if not data:  # 如果接收的是空包就跳过
                    if not self.monitor_connection(client):
                        with self.lock:
                            for node_old in self.nodes:
                                if node_old.port == info[1] and node_old.ip==info[0]:
                                    if heart_beat == 1:
                                        self.nodes.remove(node_old)
                                        #print(f"节点已离线: {node_old.name}")
                            break
                    continue
                msg_tmp = pickle.loads(data)  # 创建消息类的临时对象
                #print(f"收到消息类型: {msg_tmp.type} from {info[0]}:{info[1]}")

                if msg_tmp.type == "single_node_info":  # 处理收到的单节点信息
                    smnode:real_node = msg_tmp.content
                    heart_beat = 1
                    
                    self.get_single_node_info(smnode)
                    if not self.monitor_connection(client):
                        with self.lock:
                            for node_old in self.nodes:
                                if node_old.port == info[1] and node_old.ip==info[0]:
                                    if heart_beat == 1:
                                        self.nodes.remove(node_old)
                                        #print(f"节点已离线: {node_old.name}")
                                    break

                elif msg_tmp.type == "get_node_info":  # 处理获取节点信息的请求
                    self.ans_node_info(client, info)
                
                elif msg_tmp.type == "get_nodes_info":
                    self.ans_nodes_info(client,info)

                elif msg_tmp.type == "ans_node_info":  # 处理获取到的节点信息结果
                    smnode = msg_tmp.content
                    self.get_single_node_info(smnode)

                elif msg_tmp.type=="ans_nodes_info":#处理获取到的节点信息结果
                    self.get_nodes_info(msg_tmp.content)

                elif msg_tmp.type == "get_face_mask":
                    self.self_node.memory-=len(data)
                    while self.self_node.cpu_used_memory+len(data)>self.self_node.cpu_memory:
                        #print(f"self.self_node.cpu_used_memory+len(data)    ({len(data) })   >self.self_node.cpu_memory")
                        pass
                    self.self_node.cpu_used_memory+=len(data)
                    self.self_node.dealing_task_num+=1

                    self.ans_face_mask(client, msg_tmp.content)
                    self.self_node.cpu_used_memory-=len(data)
                    self.self_node.dealing_task_num-=1
                    self.self_node.dealed_task_num+=1
                    self.self_node.memory+=len(data)

                elif msg_tmp.type == "get_objects":
                    self.self_node.memory-=len(data)
                    self.self_node.dealing_task_num+=1
                    while self.self_node.cpu_used_memory+len(data)>self.self_node.cpu_memory:
                        #print(f"self.self_node.cpu_used_memory+len(data)    ({len(data) })   >self.self_node.cpu_memory")
                        time.sleep(2)
                        pass
                    self.self_node.cpu_used_memory+=len(data)
                    objects = self.get_objects(msg_tmp.content)
                    self.self_node.dealing_task_num-=1
                    self.self_node.dealed_task_num+=1
                    self.self_node.cpu_used_memory-=len(data)
                    msg = message()
                    msg.type = "ans"
                    msg.content = objects
                    msg_to_send = pickle.dumps(msg)
                    #print("ooooooo")
                    send_to_server(client, msg_to_send)
                    #print("已处理get_objects请求，发送结果。")
                    self.self_node.memory+=len(data)


                elif msg_tmp.type == "ans_face_mask":
                    pass  # 根据需要处理

                elif msg_tmp.type == "get_faces":
                    self.self_node.memory-=len(data)
                    while self.self_node.cpu_used_memory+len(data)>self.self_node.cpu_memory:
                        #print(f"self.self_node.cpu_used_memory+len(data)    ({len(data) })   >self.self_node.cpu_memory")
                        time.sleep(2)
                        pass
                    self.self_node.cpu_used_memory+=len(data)
                    self.self_node.dealing_task_num+=1
                    faces = self.get_faces(msg_tmp.content)
                    self.self_node.cpu_used_memory-=len(data)
                    self.self_node.dealing_task_num-=1
                    msg = message()
                    msg.type = "ans"
                    msg.content = faces
                    msg_to_send = pickle.dumps(msg)
                    send_to_server(client, msg_to_send)
                    self.self_node.dealed_task_num+=1
                    #print("已处理get_faces请求，发送结果。")
                    self.self_node.memory+=len(data)

                elif msg_tmp.type == "distribute_algorithm":
                    #print("开始分发算法任务")
                    client_dis = build_send_client("localhost", 5005)  # 发送给负载均衡模块处理
                    send_to_server(client_dis, data)
                    data = recv_from_server(client_dis)  # 接收处理结果
                    send_to_server(client, data)  # 返回处理结果
                
                elif msg_tmp.type == "get_objects_new":#此处需要重新写一个处理objects的任务
                    self.self_node.waiting_task_num+=1
                    while self.self_node.cpu_used_memory+len(data)>self.self_node.cpu_memory or self.self_node.dealing_task_num > 2:
                        time.sleep(2)
                        pass
                    self.self_node.dealing_task_num+=1
                    self.self_node.waiting_task_num-=1
                    self.self_node.cpu_used_memory+=len(data)
                    try:
                        objects = self.get_objects_new(msg_tmp.content)
                        self.self_node.cpu_used_memory-=len(data)
                        self.self_node.dealing_task_num-=1
                        self.self_node.dealed_task_num+=1
                        msg = message()
                        msg.type = "ans_get_objects"
                        msg.content = objects
                        msg.next_node=[self.self_node.ip,self.self_node.port]
                        msg_to_send = pickle.dumps(msg)
                        #print("ooooooo")
                        if(msg_tmp.next_node):
                            client_ans=build_send_client(msg_tmp.next_node[0],msg_tmp.next_node[1])
                            send_to_server(client_ans, msg_to_send)
                        else:
                            send_to_server(client, msg_to_send)
                        #print("已处理get_objects_new请求，发送结果。")
                    except:

                        client.close()
                elif msg_tmp.type == "selected_center_node":
                    if self.center_node.name!=msg_tmp.content.name:
                        
                        self.center_node=msg_tmp.content##这里将选择而出的中心节点设置上
                        self.tell_all_center_node()
                        #print(f"center node 由{self.center_node.name} 变为 {msg_tmp.content.name}")
                
                elif msg_tmp.type == "move_machine":
                    move_type = msg_tmp.content#存储的是运动行为

                elif msg_tmp.type == "shutdown":
                    
                    self.stop_tag=True
                    # self.monitor_server.close()
                    # self.monitor_server.shutdown(socket.SHUT_RDWR)
                    
                    client.close()
                    self.cleanup_connection_pool()
                    for ip, client1 in self.connection_pool.items():
                        client1.close()
                    # for thre in self.threads:
                    #     thre.join()
                    self.threads.clear()
                    #print("close threads")
                    process[self.port].terminate()
                    #print("terminate process")
                    process.pop(self.port)
                    kill_by_port(self.port)
                    pid=os.getpid()
                    os.kill(pid,signal.SIGTERM)
                    sys.exit(0)

                    break
                elif msg_tmp.type=="update_node_info":
                    update_content=msg_tmp.content
                    self.self_node.cpu_memory=update_content["cpu_memory"]
                    self.self_node.bandwidth=update_content["bandwidth"]
                    self.self_node.memory=update_content["memory"]
                    client.close()
                    break

                    


                    

                # 检查心跳连接是否存活
                
            except socket.timeout:
                #print("连接超时。")
                if not self.monitor_connection(client):
                    with self.lock:
                        for node_old in self.nodes:
                            if node_old.port == info[1] and node_old.ip==info[0]:
                                if heart_beat == 1:
                                    self.nodes.remove(node_old)
                                    #print(f"节点已离线: {node_old.name}")
                        break
            except ConnectionError as e:
                #print(f"连接错误: {e}")
                with self.lock:
                    for node_old in self.nodes:
                        if node_old.port == info[1] and node_old.ip==info[0]:
                            if heart_beat == 1:
                                self.nodes.remove(node_old)
                                #print(f"节点已离线: {node_old.name}")
                            break
                client.close()
                break
            except Exception as e:
                #print(f"处理消息时出错: {e}")
                with self.lock:
                    for node_old in self.nodes:
                        if node_old.port == info[1] and node_old.ip==info[0]:
                            if heart_beat == 1:
                                self.nodes.remove(node_old)
                                #print(f"节点已离线: {node_old.name}")
                            break
                client.close()
                break
    def tell_all_center_node(self):
        msg=message()
        msg.type="selected_center_node"
        msg.content=self.center_node
        data_to_send = pickle.dumps(msg)
        clients=[]
        for node in self.nodes:
            
            client=build_send_client(node.ip,node.port)
            send_to_server(client,data_to_send)
            clients.append(client)

    def get_monitor_server(self):
        """
        监听来自其他节点的连接，并为每个连接启动一个线程来处理消息。
        """
        while self.stop_tag==False:
            try:
                client, info = self.monitor_server.accept()
                self.connection_pool[info]=client
                #print(f"接受到来自 {info[0]}:{info[1]} 的连接。")
                # client.settimeout(5)
                thread = Thread(target=self.deal_message, args=(client, info),daemon=True)
                self.threads.append(thread)
                #print(f"当前线程数: {len(self.threads)}")
                # thread.setDaemon(True)
                thread.start()
            except Exception as e:
                print(f"监听服务器时出错: {e}")

    def scan_and_elect(self):
        """
        扫描网络并选举中心节点，然后连接到中心节点。
        """
        self.scan_network()
        self.connect_to_center_node()

    def add_center_node_attribute(self, center_node):
        """
        添加center_node属性，存储选举产生的中心节点的信息。
        """
        with self.lock:
            self.center_node = center_node
            #print(f"已设置中心节点: {self.center_node.name}")

    def monitor_real_node(self):
        time.sleep(10)
        while True:
            if(self.center_node.name!=self.self_node.name):
                continue
            cur_time=time.time()
            
            for idx in range(len(self.nodes)-1,-1,-1):
                node=self.nodes[idx]
                if cur_time-node.time>20:
                    #print(f"cur time is {cur_time} ;node time is {node.time}")
                    self.nodes.pop(idx)
                    #print(f"{node.name} 因超时退出")
                    # self.connection_pool[node.name].close()
                    # self.connection_pool.pop(node.name)
                    # self.cleanup_connection_pool()
            time.sleep(10)


    





def _run(idx:int,bp:int):
    c=ControlCenter(idx,bp)
    while True:
        if c.stop_tag:
            return 0
        time.sleep(2)
def add_one_machine(port:int,cpu_memory:int,bandwidth:int,memory:int):
    try:
        mp.set_start_method('spawn',force=True)
        p = mp.Process(target=_run,args=(0,port),daemon=True)
        p.start()
        p.join()
    except:
        #print("节点添加失败")
        pass
def start_all_vc(num):
    ap = argparse.ArgumentParser()
    ap.add_argument('-n','--nodes',type=int,default=num)
    ap.add_argument('-p','--base-port',type=int,default=60000)
    arg = ap.parse_args()
    mp.set_start_method('spawn',force=True)
    ps=[]
    for i in range(arg.nodes):
        p = mp.Process(target=_run,args=(i,arg.base_port),daemon=True)
        process[60000+i]=p
        p.start(); ps.append(p)
    #print(f"Started {arg.nodes} nodes on ports {arg.base_port}-{arg.base_port+arg.nodes-1}")
    
    while len(process)>0 and ps!=[]:
        #print(f"there are {len(process)} processes in process   and {len(ps)} in ps ")
        for i in range(len(ps)-1,-1,-1):
            p=ps[i]
            if p.is_alive()==False:
                ps.pop(i)
        
        time.sleep(2)
    # for p in ps: p.join()
    return ps

# if __name__=='__main__': 
#     start_all_vc(5)