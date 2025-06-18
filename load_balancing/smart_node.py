
import gc
import time
import sys

import psutil
import socket
import re
import time

import subprocess

import random
import pickle
from server_client import *

# x=random.randint(1,800)
# y=random.randint(1,800)

# def cpu_usage():
#     usage=psutil.cpu_percent(interval=1)
#     print(usage)
#     return usage





# def get_non_loopback_ip():
#     for iface_name, iface_addresses in psutil.net_if_addrs().items():
#         for address in iface_addresses:
#             if address.family == socket.AF_INET and address.address != "127.0.0.1":
#                 return address.address
#     return "127.0.0.1"  # 如果没有找到非回环地址




# def get_all_info(hostname):
#     info=[]
#     free_memory=psutil.virtual_memory().free
#     print("free memory is : "+str(free_memory))
#     total_memory = psutil.virtual_memory().total
#     print("total memory is : "+str(total_memory))
#     usage = cpu_usage()
#     print(f"CPU Usage: {usage:.2f}%")
#     info.append(hostname)
#     info.append(y)
#     info.append(x)
#     info.append(free_memory)
#     info.append(usage)
#     return info

# def main():
#     try:
#         hostname=socket.gethostname()
#         ip=get_non_loopback_ip()
        
#         info=get_all_info(ip)
#         dest_ip="192.168.137.1"
#         plt_ip="192.168.137.1"
#         dest_port=10000
#         plt_port=5001#监视程序
#         client=socket.socket()
#         addr=(dest_ip,plt_port)
#         client.connect(addr)
#         while True:
#             info=get_all_info(ip)
#             print(info)
#             data_to_send=list_to_binary(info)
#             print(data_to_send)
#             send_to_server(client,data_to_send)
#             time.sleep(2)
#             #data=recv_from_server(client)
            
#         sys.exit(0)
    
#     except KeyboardInterrupt:
#         print("exiting...")
#         sys.exit(0)


# if __name__ == '__main__':
#     main()


dealed_tasks=0
connection_pool={}
class real_node:
    def __init__(self,port):
        self.position=random.randint(1,800),random.randint(1,800)
        # self.name=get_non_loopback_ip()
        self.radius = 100
        self.neighbors = []
        self.ip="192.168.1.7"
        self.port=port
        self.name=self.ip+":"+str(self.port)
        print(self.name)
        self.deal_speed=30
        self.bandwidth=200000
        self.left_bandwidth=self.bandwidth#存储剩余带宽
        self.memory=200000
        self.signal=100
        self.receive_tag=0#判断是否有任务 在传输中
        self.send_tag=0#判断是否有任务在发送中
        self.connection=[]#存储已连接的节点
        self.if_change_signal=0#是否随机变化信号值
        self.tasks=[]#存储任务列表
        self.group=0#节点默认所属集群是0，只有相同的集群才可以创建连接
        self.cpu_memory=100000#节点的cpu运行内存
        self.cpu_used_memory=0#节点已使用cpu运行内存
        self.cpu_used_rate=int(self.cpu_used_memory/self.cpu_memory)*100#计算cpu使用率
        self.algorithm_type=1#选择算法类型，1表示方氏算法，2表示马氏算法
        self.weight=0#存储节点的分配任务时的权重
        self.initial_compute_capacity = self.cpu_memory
        self.initial_storage_capacity = self.memory
        self.compute_capacity = self.cpu_memory
        self.storage_capacity = self.memory
        self.active_count = 0
        self.tasks_duration = self.cpu_used_memory
        self.dealed_task_num=0#统计处理了多少任务
        self.dealing_task_num=0
        self.works=[]
        self.distribute_time=0
        self.refresh_info()
    def refresh_info(self):
    
        if(self.port==5002):
        
            
            freq = psutil.cpu_freq()
            self.deal_speed=freq.current
            self.left_bandwidth=self.bandwidth-get_current_bandwidth('wlan0')
            get_bandwidth_info('wlan0')
            #################获取磁盘存储###############
            try:
                memory_usage = psutil.disk_usage('/')
                memory_total = memory_usage.total / (1024**3)  # 转换为 GB
                memory_used = memory_usage.used / (1024**3)    # 转换为 GB
                memory_free = memory_usage.free / (1024**3)    # 转换为 GB
                memory_percent = memory_usage.percent

                # print(f"分区: {'/'}")
                # print(f"总空间: {memory_total:.2f} GB")
                # print(f"已用空间: {memory_used:.2f} GB")
                # print(f"剩余空间: {memory_free:.2f} GB")
                # print(f"使用率: {memory_percent}%")
                self.memory=memory_free
            except Exception as e:
                pass
                # print(f"无法获取分区 {'/'} 的信息: {e}")

            ################获取内存信息###################
            cpu_usage = psutil.cpu_percent(interval=1)  # 计算1秒的CPU使用率
            # print(f"CPU 使用率: {cpu_usage:.2f}%")

            # 获取内存使用情况
            cpu_memory_info = psutil.virtual_memory()
            cpu_used_memory = cpu_memory_info.used / (1024**3)    # 转换为 GB
            cpu_free_memory = cpu_memory_info.available / (1024**3)  # 转换为 GB
            cpu_memory_percent = cpu_memory_info.percent
            self.cpu_memory=cpu_free_memory
            self.cpu_used_memory=cpu_used_memory
            self.cpu_used_rate=self.cpu_used_memory/(self.cpu_memory+self.cpu_used_memory)-0.3
            # print(f"已用内存: {cpu_used_memory:.2f} GB")
            # print(f"剩余内存: {cpu_free_memory:.2f} GB")
            # print(f"内存使用率: {cpu_memory_percent:.2f}%")
            self.connection=[]
            for ip in connection_pool:
                self.connection.append(ip)
        else:
            freq = psutil.cpu_freq()
            self.deal_speed=freq.current/10
            self.left_bandwidth=self.bandwidth-get_current_bandwidth('wlan0')
            get_bandwidth_info('wlan0')
            #################获取磁盘存储###############
            try:
                memory_usage = psutil.disk_usage('/')
                memory_total = memory_usage.total / (1024**3)  # 转换为 GB
                memory_used = memory_usage.used / (1024**3)    # 转换为 GB
                memory_free = memory_usage.free / (1024**3)    # 转换为 GB
                memory_percent = memory_usage.percent

                # print(f"分区: {'/'}")
                # print(f"总空间: {memory_total:.2f} GB")
                # print(f"已用空间: {memory_used:.2f} GB")
                # print(f"剩余空间: {memory_free:.2f} GB")
                # print(f"使用率: {memory_percent}%")
                self.memory=memory_free/10
            except Exception as e:
                pass
                # print(f"无法获取分区 {'/'} 的信息: {e}")

            ################获取内存信息###################
            cpu_usage = psutil.cpu_percent(interval=1)  # 计算1秒的CPU使用率
            # print(f"CPU 使用率: {cpu_usage:.2f}%")

            # 获取内存使用情况
            cpu_memory_info = psutil.virtual_memory()
            cpu_used_memory = cpu_memory_info.used / (1024**3)    # 转换为 GB
            cpu_free_memory = cpu_memory_info.available / (1024**3)  # 转换为 GB
            cpu_memory_percent = cpu_memory_info.percent
            self.cpu_memory=cpu_free_memory/10
            self.cpu_used_memory=cpu_used_memory/10
            self.cpu_used_rate=self.cpu_used_memory/(self.cpu_memory+self.cpu_used_memory)+0.07*self.dealing_task_num
            # print(f"已用内存: {cpu_used_memory:.2f} GB")
            # print(f"剩余内存: {cpu_free_memory:.2f} GB")
            # print(f"内存使用率: {cpu_memory_percent:.2f}%")
            self.connection=[]
            for ip in connection_pool:
                self.connection.append(ip)


def get_non_loopback_ip():
    for iface_name, iface_addresses in psutil.net_if_addrs().items():
        for address in iface_addresses:
            if address.family == socket.AF_INET and address.address != "127.0.0.1":
                return address.address
    return "127.0.0.1"  # 如果没有找到非回环地址


# def get_non_loopback_ip():
#     IP=socket.gethostbyname(socket.gethostname())
#     return IP


def get_interface_max_bandwidth(interface):
    try:
        # 运行 ethtool 命令，获取网口带宽
        result = subprocess.run(f"ethtool {interface} | grep Speed", shell=True, capture_output=True, text=True)
        output = result.stdout.strip()
        if output:
            # print(output)
            speed = output.split(":")[1].strip()
            # print(f"接口 {interface} 的最大带宽: {speed}")
            return speed
        else:
            # print(f"无法获取 {interface} 的带宽信息。")
            return 0
    except Exception as e:
        print(f"错误: {e}")
        return 0

def get_network_bandwidth_once(interface, interval=1):
    """
    获取实时的网络传输速率 (发送和接收)。
    """
    net_before = psutil.net_io_counters(pernic=True)
    time.sleep(interval)  # 等待时间间隔
    net_after = psutil.net_io_counters(pernic=True)

    if interface not in net_before or interface not in net_after:
        print(f"接口 {interface} 不存在，请检查接口名称。")
        return None, None

    sent_bytes = net_after[interface].bytes_sent - net_before[interface].bytes_sent
    recv_bytes = net_after[interface].bytes_recv - net_before[interface].bytes_recv

    sent_mbps = (sent_bytes * 8) / (1024 * 1024 * interval)  # 发送速度 Mbps
    recv_mbps = (recv_bytes * 8) / (1024 * 1024 * interval)  # 接收速度 Mbps

    return sent_mbps, recv_mbps

def get_remaining_bandwidth(interface, interval=1):
    """
    获取剩余带宽 (最大带宽减去当前使用的带宽)。
    """
    max_bandwidth = get_interface_max_bandwidth(interface)
    if max_bandwidth is None:
        print("无法获取最大带宽。")
        return

    sent_mbps, recv_mbps = get_network_bandwidth_once(interface, interval)

    if sent_mbps is None or recv_mbps is None:
        print("无法获取网络传输速率。")
        return

    # 计算剩余带宽
    used_bandwidth = max(sent_mbps, recv_mbps)  # 使用了的最大带宽
    remaining_bandwidth = max_bandwidth - used_bandwidth

    # print(f"接口: {interface} 的最大带宽: {max_bandwidth} Mbps")
    # print(f"发送: {sent_mbps:.2f} Mbps, 接收: {recv_mbps:.2f} Mbps")
    # print(f"当前剩余带宽: {remaining_bandwidth:.2f} Mbps")
    return remaining_bandwidth

# 调用函数获取剩余带宽 (假设网口名为 'eth0')
def get_max_bandwidth(interface):
    try:
        # 运行 iw 命令获取无线网卡的能力信息
        result = subprocess.run(f"iw dev {interface} info", shell=True, capture_output=True, text=True)
        output = result.stdout.strip()
        
        # 提取频率 (GHz) 和信道宽度 (MHz)
        freq_match = re.search(r"channel [0-9]+ \((\d+)\s+MHz\)", output)
        width_match = re.search(r"width:\s+(\d+)\s+MHz", output)
        
        if freq_match and width_match:
            frequency = int(freq_match.group(1))  # 提取频率
            width = int(width_match.group(1))  # 提取信道宽度

            # 根据频率和信道宽度判断最大带宽
            if 2400 <= frequency <= 2500:  # 2.4 GHz 频段
                if width == 20:
                    return 150
                elif width == 40:
                    return 300
            elif 4900 <= frequency <= 5900:  # 5 GHz 频段
                if width == 20:
                    return 433
                elif width == 40:
                    return 866
                elif width == 80:
                    return 1300
        else:
            # print(f"无法解析 {interface} 的最大带宽信息。")
            return 0
    except Exception as e:
        # print(f"错误: {e}")
        return 0
def get_current_bandwidth(interface):
    try:
        # 运行 iw 命令获取无线网卡的当前连接信息
        result = subprocess.run(f"iw dev {interface} link", shell=True, capture_output=True, text=True)
        output = result.stdout.strip()
        
        if "bitrate" in output:
            lines = output.splitlines()
            for line in lines:
                if "bitrate" in line:
                    # 使用正则表达式提取比特率中的数字部分
                    match = re.search(r"bitrate:\s+([0-9.]+)", line)
                    if match:
                        bitrate_str = match.group(1)  # 获取比特率的数字部分作为字符串
                        return float(bitrate_str)  # 转换为 float 类型
        else:
            # print(f"无法获取 {interface} 的当前带宽信息。")
            return 0
    except Exception as e:
        print(f"错误: {e}")
        return 0
def get_bandwidth_info(interface):
    max_bandwidth = get_max_bandwidth(interface)
    current_bandwidth = get_current_bandwidth(interface)
    # print(f"接口: {interface}")
    # print(f"最大带宽: {max_bandwidth}")
    if current_bandwidth != 0:
        print(f"当前带宽: {current_bandwidth} Mbps")
    else:
        print("未能获取当前带宽信息。")



def get_my_node(port):
    my_node=real_node(port)
    freq = psutil.cpu_freq()
    my_node.deal_speed=freq.current
    my_node.bandwidth=get_max_bandwidth('wlan0')
    my_node.left_bandwidth=my_node.bandwidth-get_current_bandwidth('wlan0')
    get_bandwidth_info('wlan0')
    #################获取磁盘存储###############
    try:
        memory_usage = psutil.disk_usage('/')
        memory_total = memory_usage.total / (1024**3)  # 转换为 GB
        memory_used = memory_usage.used / (1024**3)    # 转换为 GB
        memory_free = memory_usage.free / (1024**3)    # 转换为 GB
        memory_percent = memory_usage.percent

        # print(f"分区: {'/'}")
        # print(f"总空间: {memory_total:.2f} GB")
        # print(f"已用空间: {memory_used:.2f} GB")
        # print(f"剩余空间: {memory_free:.2f} GB")
        # print(f"使用率: {memory_percent}%")
        my_node.initial_storage_capacity=memory_total
        my_node.memory=memory_free
    except Exception as e:
        print(f"无法获取分区 {'/'} 的信息: {e}")

    ################获取内存信息###################
    cpu_usage = psutil.cpu_percent(interval=1)  # 计算1秒的CPU使用率
    # print(f"CPU 使用率: {cpu_usage:.2f}%")

    # 获取内存使用情况
    cpu_memory_info = psutil.virtual_memory()
    cpu_total_memory = cpu_memory_info.total / (1024**3)  # 转换为 GB
    cpu_used_memory = cpu_memory_info.used / (1024**3)    # 转换为 GB
    cpu_free_memory = cpu_memory_info.available / (1024**3)  # 转换为 GB
    cpu_memory_percent = cpu_memory_info.percent
    my_node.cpu_memory=cpu_free_memory
    my_node.initial_compute_capacity=cpu_total_memory
    my_node.cpu_used_memory=cpu_used_memory
    # print(f"总内存: {cpu_total_memory:.2f} GB")
    # print(f"已用内存: {cpu_used_memory:.2f} GB")
    # print(f"剩余内存: {cpu_free_memory:.2f} GB")
    # print(f"内存使用率: {cpu_memory_percent:.2f}%")
    return my_node




def send_my_info(client,node:real_node):
    msg_tmp=message()
    msg_tmp.type="single_node_info"
    msg_tmp.content=node
    msg=pickle.dumps(msg_tmp)
    send_to_server(client,msg)
    time.sleep(2)

def is_client_alive(client):
    try:
        # 使用 select 检查是否有异常或关闭的连接
        readable, _, _ = select.select([client], [], [], 0)
        if readable:
            return False  # 如果可读，说明可能有错误或连接断开
        return True
    except (socket.error, socket.timeout) as e:
        print(f"检测连接时发生异常: {e}")
        return False

def cleanup_connection_pool():
    # 收集所有无效的 IP
    try:
        ips_to_remove = [ip for ip, client in connection_pool.items() if not is_client_alive(client)]
        for ip in ips_to_remove:
            client = connection_pool.pop(ip)
            client.close()  # 关闭连接
            print(f"已删除无效连接: {ip}")
    except:
        pass
    # 删除无效的客户端连接
    
def send_myself_info(IP,PORT):
    # HOST=socket.gethostbyname("192.168.137.1")
    HOST=socket.gethostbyname(IP)
# HOST=socket.gethostbyname('192.168.137.1')
    # PORT=5002
    client=socket.socket(socket.AF_INET,socket.SOCK_STREAM)

    client.connect((HOST,PORT))
    
    connection_pool[IP]=client
    node=get_my_node(PORT)
    while 1: 
        node.refresh_info()
        cleanup_connection_pool()
        send_my_info(client,node)
        time.sleep(5)
        # try:
        #     # 发送一个空的心跳包以检测连接是否仍然存活
            
        # except (socket.error, socket.timeout):
        #     try:
        #         client.connect((HOST,PORT))
        #     except:
        #         pass



class real_tasks():
    def __init__(self):
        self.name="task"
        self.start_time=time.time()
        self.end_time=0
        self.owner=None
        self.content=[]#存储任务的内容
        self.task_type=""#存储任务的类型
        self.if_divide=0#是否需要分割
        self.sons:list[self]=[]#任务分割交给后人了！
        self.father=[]
        self.size=0
        pass
    def get_content(self,content):
        self.content=content
        self.size=self.get_total_size(content)
    def get_total_size(self,obj, seen=None):
    #"""递归计算对象所占的总内存大小"""
        if seen is None:
            seen = set()
        obj_id = id(obj)
        if obj_id in seen:
            return 0
        seen.add(obj_id)
        size = sys.getsizeof(obj)
        if isinstance(obj, (list, tuple, set, frozenset)):
            size += sum(self.get_total_size(item, seen) for item in obj)
        elif isinstance(obj, dict):
            size += sum(self.get_total_size(k, seen) + self.get_total_size(v, seen) for k, v in obj.items())
        return size

# main()