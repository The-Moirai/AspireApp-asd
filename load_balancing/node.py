from server_client import *
from threading import Thread
from smart_node import real_node
import pickle

query_msg = message()
query_msg.type = "get_nodes_info"
query_data = pickle.dumps(query_msg)
client1 = build_send_client("192.168.27.130", 5002)
send_to_server(client1, query_data)
ans = recv_from_server(client1)
ans_tmp = pickle.loads(ans)
xhn_nodes = ans_tmp.content
for node in xhn_nodes:
    print(node.cpu_used_rate)