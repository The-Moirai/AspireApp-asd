from smart_node import real_node
from smoothweight import *
from server_client import *
import pickle
#这里本来也打算通过消息总线方式实现传参，这样就可以实现模块化，可以单独启动关闭
#考虑到效率问题，暂时没有使用
#5005端口留给算法模块

def create_listener():
        def get_node_info(ip):
                client=build_send_client(ip,5002)#创建通信客户端，指定5002端口，向总线获取节点信息
                msg=message()
                msg.type="get_nodes_info"
                data=pickle.dumps(msg)
                send_to_server(client,data)
                ans=recv_from_server(client)
                ans_tmp=pickle.loads(ans)
                nodes=ans_tmp.content
                return nodes
        def deal_client(client):
                data=recv_from_server(client)
                if not data:
                        return 0
                msg=pickle.loads(data)
                msg_type=msg.type
                if msg_type=="distribute_algorithm":
                        if msg.content is  None:
                                nodes=get_node_info(msg.next_node)
                        else:
                                nodes=msg.content
                                #如果消息文件里没有给节点信息，就自己查询。这里规定，发过来查询算法结果的消息，
                                #msg.next_node必须填入center_node的ip
                        if len(nodes)==0:
                                print("there are no nodes free now!")
                                return 0
                        node=distribute(nodes)
                        #算法可以有多种方法，示例使用的是选择出一个节点，来发送当前的任务，在服务端处理的方式就是对当前
                        #收到的节点发送。当然也可以设计返回一个映射关系，发送端发送任务信息和节点信息集合
                        #此处返回对应表，最后服务端根据映射表发送任务。
                        ans_msg=message()
                        ans_msg.type="selected_node"
                        ans_msg.content=node
                        ans_data=pickle.dumps(ans_msg)
                        print(f"distribute mode returned a node :{node.name}")
                        send_to_server(client,ans_data)
                        return 1 #这一块需要讨论下，到底是把任务发过来在这里直接分配，还是啊在这里生成分配的的策略返回分配
                        #个人建议是在这里直接分配，发过来的消息可以直接包括节点信息和任务，可以节约通信成本，并且，
                        #本处的端口使用的是localhost，其他节点无法访问，也就意味着只支持本机的访问和查询
                        #因此，在此处直接分配比较好


        clients=[]
        threads=[]
        server=socket.socket(socket.AF_INET,socket.SOCK_STREAM)
        server.bind(("localhost",5005))
        server.listen(10)# 这里创建接收信息的服务 
        while 1:
                client,info=server.accept()
                clients.append(client)
                print(f"{info[0]}发来了一个查询信息")
                thread=threading.Thread(target=deal_client,args=(client,))
                threads.append(thread)
                
                thread.start()



def distribute(nodes:list[real_node]):



        smooth_weight_poll_server = SmoothWeightPollServer(nodes)
        node=smooth_weight_poll_server.sort_weight_real_node()
        #接下来扫描所有的连接，若没有与对应节点连接则创建连接，若有连接直接发送
        #考虑到消息总线中获取连接比较方便，这里只返回筛选出来的任务分配的节点
        #后续者可根据需求修改此处的分配规则以及消息总线中的分配函数
        return node 



create_listener()
