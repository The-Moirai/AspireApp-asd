import numpy as np

def loadBalanceReward(vms):
    vms = np.array(vms)
    n = len(vms)
    mean = np.mean(vms)
    if mean == 0:
        return 1  # 完全空闲，视为“均衡”

    gini = np.sum(np.abs(vms[:, None] - vms[None, :])) / (2 * n**2 * mean)
    reward = 1 - gini  # gini 越小越均衡 → reward 趋近于 1
    return reward * 2 - 1  # 映射到 [-1, 1]


vms = [1, 2, 3, 4, 5, 6, 7, 8, 9, 2, 3, 4, 5]
vms2 = [3, 3, 4, 3, 4, 3, 3, 4, 3, 4, 3, 4, 3]

r1 = loadBalanceReward(vms)
r2 = loadBalanceReward(vms2)

print(r1, r2)