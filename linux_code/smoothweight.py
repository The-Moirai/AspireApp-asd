import random
from typing import List
class SmoothWeightPollServer:
    def __init__(self,nodes):
        self.slist=nodes
        self.total_speed = 0
        self.total_bandwidth = 0
        self.total_memory = 0
        self.get_all_value()
    def set_weight(self,node,total_speed: int, total_bandwidth: int, total_memory: int):
        #设置各个节点自身的权重，用于分配算法
        node.weight = (node.deal_speed / total_speed) + (node.left_bandwidth / total_bandwidth) + (node.memory / total_memory)-node.cpu_used_rate

    def get_all_value(self):
        
        try:
            for node in self.slist:
                self.total_speed += node.deal_speed
                self.total_bandwidth += node.left_bandwidth
                self.total_memory += node.memory
        except:
            pass

    def sort_weight(self):
        for node in self.slist:
            node.set_weight(self.total_speed, self.total_bandwidth, self.total_memory)

        self.slist.sort(key=lambda node: node.weight,reverse=False)
    def sort_weight_real_node(self):
        self.slist.sort(key=lambda node: node.cpu_used_rate,reverse=False)
        print(f"0 is {self.slist[0].name}\n,all is {self.slist}")
        return self.slist[0]
    def assignable(self, node, task_data: int) -> bool:
        return node.memory > task_data

    def get_server(self, task_data: int) -> str:
        for node in self.slist:
            if self.assignable(node, task_data):
                node.memory -= task_data
                self.total_memory -= task_data
                self.sort_weight()
                return f"{node.name} 被分配了"

        return "分配失败"

    def task_complete(self, address: str, task_data: int) -> str:
        for node in self.slist:
            if node.name == address:
                node.memory += task_data
                self.total_memory += task_data
                self.sort_weight()
                return f"{address} 完成了一个任务"

        return "应该不会出现循环完还未找到的情况吧"
