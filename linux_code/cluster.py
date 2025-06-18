import math
import random
import socket
import json

from server_client import message, build_send_client, send_to_server, recv_from_server
import pickle

# query_msg = message()
# query_msg.type = "get_nodes_info"
# query_data = pickle.dumps(query_msg)
# # client = build_send_client("118.25.27.210", 5006)
# client = build_send_client("127.0.0.1", 5006)
# send_to_server(client, query_data)
# ans = recv_from_server(client)
# ans_tmp = pickle.loads(ans)
# nodes = ans_tmp.content


# for node in nodes:
#     print(node.name, node.cpu_used_rate, [node1.name for node1 in node.connection], [task.name for task in node.tasks],
#           [(task.name, task.size) for task in node.dealing_tasks], node.dealed_task_num)
#     # print([task.name for task in node.tasks], [(task.name, task.size) for task in node.dealing_tasks],
#     #       node.dealed_task_num)


# 资源态势子图
def get_resource_situation_map(nodes):
    total_cpu = 0.0
    total_memory = 0
    total_bandwidth = 0.0

    for node in nodes:
        total_cpu += node.cpu
        total_memory += node.memory
        total_bandwidth += node.bandwidth
    resource_situation_map = [[f"computing_resource：{round(total_cpu, 2)} GHz"],
                              [f"storage_resource:{round(total_memory, 2)} Mb"],
                              [f"communication_resource:{round(total_bandwidth, 2)} Mbps"]]

    return resource_situation_map


#  计算两个节点间的欧氏距离
def calculate_distance(node1, node2):
    return math.sqrt((node1.x - node2.x) ** 2 + (node1.y - node2.y) ** 2)


# 初始化邻居节点
def initialize_neighbors(nodes):
    for node in nodes:
        node.neighbors=[]
        for node2 in nodes:
            if node.name != node2.name:
                
                distance = calculate_distance(node, node2)
                #print(f"开始计算{node.name}和{node2.name}之间的距离--->{distance}，半径为{node.radius}")
                if distance <= node.radius:
                    node.add_neighbors(node2)
        # print(f"{node.name}：{[node1.name for node1 in node.neighbors]}")


# 根据节点感知半径分簇
def cluster_nodes_by_radius(nodes):
    # nodeDict = []
    # for i in range(len(nodes)):
    #     nodeDict.p nodes[i]
    clusters = []
    visited = [False] * len(nodes)

    for i in range(len(nodes)):
        if visited[i]:
            continue

        cluster = [nodes[i]]
        visited[i] = True

        for j in range(i + 1, len(nodes)):
            if visited[j]:
                continue
            if calculate_distance(nodes[i], nodes[j]) <= nodes[i].radius:
                cluster.append(nodes[j])
                visited[j] = True

        clusters.append(cluster)
    return clusters


# 簇内资源汇总
def get_resource_summary_for_clusters(cluster):
    total_cpu = sum([ncp.cpu for ncp in cluster])
    total_memory = sum([ncp.memory for ncp in cluster])
    total_bandwidth = sum([ncp.bandwidth for ncp in cluster])
    return total_cpu, total_memory, total_bandwidth


# 簇信息
def print_cluster_info(clusters):
    for i, cluster in enumerate(clusters):
        node_info = ",".join([f"{node.name}" for node in cluster])
        #print(f"Cluster{i + 1} - nodeInfo: {node_info}")

        # print(node_info)
        total_cpu, total_memory, total_bandwidth = get_resource_summary_for_clusters(cluster)

        #print(f"cluster_computing_resource: {round(total_cpu, 2)} GHz")
        #print(f"cluster_storage_resource: {round(total_memory, 2)} MB")
        #print(f"cluster_communication resource: {round(total_bandwidth, 2)} Mbps")
        if i < len(clusters) - 1:
            print()


# 拓展一跳邻居
def explore_neighbors(node, visited):
    if node in visited:
        return set()

    visited.add(node)
    all_neighbors = set(node.neighbors)

    for neighbor in node.neighbors:
        all_neighbors.update(explore_neighbors(neighbor, visited))
    return all_neighbors


# def get_node_neighbors(nodes, start_node=None):
#     if not nodes:
#         return None, set()
#
#     if start_node is None:
#         start_node = random.choice(nodes)
#     elif start_node not in nodes:
#         raise ValueError("指定节点不存在")
#     visited = set()
#     all_neighbors = explore_neighbors(start_node, visited)
#     return start_node, all_neighbors
# 获取一跳可达邻居
def get_node_neighbors(nodes, node=None):
    if node is None:
        node = random.choice(nodes)
    direct_neighbors = set(node.neighbors)
    neighbors_of_neighbors = set()

    for neighbor in direct_neighbors:
        for second_degree_neighbor in neighbor.neighbors:
            if second_degree_neighbor not in direct_neighbors and second_degree_neighbor != node:
                neighbors_of_neighbors.add(second_degree_neighbor)
    return node, {node} | direct_neighbors | neighbors_of_neighbors


# 对一跳可达邻居分簇
def categorize_nodes_by_computation(all_neighbors):
    total_computation = sum(node.cpu for node in all_neighbors)
    avg_computation = total_computation / len(all_neighbors)
    # print(f"avg: {avg_computation}")
    high_computation_nodes = [node.name for node in all_neighbors if node.cpu >= avg_computation]
    low_computation_nodes = [node.name for node in all_neighbors if node.cpu < avg_computation]
    return high_computation_nodes, low_computation_nodes


# # # ----------------------------------------------------------------------------------------------------------
# print(get_resource_situation_map(nodes))
# print("=" * 50, "邻接表", "=" * 50)
# initialize_neighbors(nodes)
# print("=" * 50, "节点分簇", "=" * 50)
# result_cluster = cluster_nodes_by_radius(nodes)



# print_cluster_info(result_cluster)
# print("=" * 50, "一跳可达", "=" * 50)
# start_node, neighbors = get_node_neighbors(nodes, node=nodes[1])
# print(f"choise_node: {start_node.name}")
# print(f"first_level_neighbors: {[neighbor.name for neighbor in neighbors]}")
# # all_neighbors = get_node_neighbors(nodes[0])
# # print(nodes[0].name)
# # print(f"所有一跳可达邻居: {[neighbor.name for neighbor in all_neighbors]}")
# #
# #
# total_cpu, total_memory, total_bandwidth = get_resource_summary_for_clusters(neighbors)
# print(f"computing_resource: {round(total_cpu, 2)} GHz")
# print(f"storage_resource: {round(total_memory, 2)} MB")
# print(f"communication resource: {round(total_bandwidth, 2)} Mbps")
# high_computation_nodes, low_computation_nodes = categorize_nodes_by_computation(neighbors)
# print(f"    high_computation_nodes: {high_computation_nodes}")
# # high_cpu, high_memory, high_bandwidth = get_resource_summary_for_clusters(high_computation_nodes)
# # print(f"computing_resource: {round(high_cpu, 2)} GHz")
# # print(f"storage_resource: {round(high_cpu, 2)} MB")
# # print(f"communication resource: {round(high_bandwidth, 2)} Mbps")
# print(f"    low_computation_nodes: {low_computation_nodes}")


