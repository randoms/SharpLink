SharpLink
==========

***SharpLink*** is a socket proxy. It can bind other computer's port to local port. You can directly access your local port to communicate with other computers. This software is based on [Tox](https://github.com/irungentoo/toxcore "toxcore") project

So why it is useful?
You can use it to play LAN games with your friends.
SSH to your office computer from home, and many many things that used to be possible only in LANs.

## Compile
### Linux
1. Install toxcore, the instruction can be found [here](https://github.com/irungentoo/toxcore/blob/master/INSTALL.md)
2. Install MonoDevelop
   ```
   sudo apt-get install mono-complete
   ```
3. Compile, you have to set build type to Debug POSIX or Release POSIX to build it. If you are facing with errors of dll not match, you have to reinstall depent libraries.

### Windows
clone this project

    git clone https://github.com/randoms/SharpLink
    git checkout master

If you simply want to use it, you do not need to compile it. It is already compiled in dist folder. SharpLink.exe is the execute. Be careful, the .dll files in disk folder are also needed.

How to compile

open SharpLink.sln with visual studio. You can compile it with visual studio.

## Usage
For example if computer B wants to connect to computer A's port 3128. But computer A and computer B are not in the same LAN.
A is connected to the Internet via a router.
First run server program on computer A

    SharpLink.exe

A file named 'server.log' will be created in current folder. File contents would be

    ID: C519671087E6E2A2EE24AE681CD1D494F8F2D2727A2E8380A1567C62F339D44E1DD28E8AF571
    Server listening on 51311
    From Server 51311:tox is connected.

Record the long string after ID:. This is the ID of computer A. We will use it later.

Run the following command on computer B

    SharpLink 9990 C519671087E6E2A2EE24AE681CD1D494F8F2D2727A2E8380A1567C62F339D44E1DD28E8AF571 127.0.0.1 3128

The long string above is computer A's ID. 9990 is computer B's local port. 127.0.0.1 is the ip address computer B want to connect with via computer A. 3128 is the port computer B want to connect.
After running that, a filed named 'client.log' will be created in your current folder.

    If connected successfully, file contents would be

    ID: D2D336CA480D19ED7C1EBD90F4500863EE2577853C1CEF7AEC3141D509A0F042110249C1CE47
    Server listening on 50895
    Waiting socket
    From Server 50895:tox is connected.
    Waiting socket

You can now connect to you local port 9990, it feels as if you have connected to computer A's port 3128. Be careful it may takes up to one minute to start a connection.

Command format

    SharpLink local_port target_id target_ip target_port

    local_port  local port
    target_id   target computer's ID, the long string in terminal output
    target_ip   the IP address you want target computer to connect with. If you just want to connect with target computer, then it would be 127.0.0.1
    target_port target port you want to connect

    If you run SharpLink without any parameters, it will be running in server mode.

How this is achieved

In fact this process is simple. We create a socket both on local computer and remote computer. The two socket communicate via tox. And it seems as if we have binded the remote computer's port to local port.

##Licence
  You are free to do anything...

***SharpLink*** 简单来说就是一个端口代理软件。它能够把任意远程电脑的端口映射到本地端口。这样你就可以通过直接连接本地端口来和远程的电脑通信。这是一个p2p软件所以连接速度要比vpn之类的快很多。这个软件是建立在[Tox](https://github.com/irungentoo/toxcore "toxcore")项目之上的。

有什么用？
有很多用处，你可以利用这个和不同局域网的人玩局域网游戏。远程控制不在同一个局域网内的电脑。连接不在同一个局域网的ftp服务器。总之，几乎所有被局域网限制的应用都可用这个来解除限制。

如果你对这个软件有什么建议欢迎在issue里面提出

## 编译安装

### Linux
1. 安装toxcore,这里有详细的安装方法
2. 安装MonoDevelop,打开IDE之后编译.注意要把变异类型设置成Debug POSIX 或者 Release POSIX版本.


### Windows

获取主程序

    git clone https://github.com/randoms/SharpLink
    git checkout master

如果只是使用的话是可以不用编译的。在 dist 文件夹中有已经编译好的文件，其中SharpLink.exe是最终的程序. 注意dist文件夹中的dll文件也是必须的，如果想要在其他地方使用一定要带上dll文件。

如何编译

用 visual studio 打开SharpLink.sln, 点击visual studio 内的编译就行了。


##使用方法
现在假如B电脑想要连接到A电脑的3128端口，但是A和B并不在一个局域网中，A经过路由器连接到互联网中。
首先在A电脑上运行服务端程序

    SharpLink.exe

    此时在当前目录下会创建一个叫做server.log的文件,其内容如下

    ID: C519671087E6E2A2EE24AE681CD1D494F8F2D2727A2E8380A1567C62F339D44E1DD28E8AF571
    Server listening on 51311
    From Server 51311:tox is connected.

记录下ID:后面的一长串字符，这个是A电脑的ID，这个以后会用到。

在B电脑上执行

    SharpLink 9990 C519671087E6E2A2EE24AE681CD1D494F8F2D2727A2E8380A1567C62F339D44E1DD28E8AF571 127.0.0.1 3128

中间一长串就是A的ID， 9990 代表本地端口， 127.0.0.1 是B想要A连接的IP，3128是B想要A连接的端口。整个的效果就是A把自己的3128端口映射到B的9990端口上.

    此时在当前文件夹下会创建一个叫做client.log的文件,如果成功连接文件内容如下
    
    ID: D2D336CA480D19ED7C1EBD90F4500863EE2577853C1CEF7AEC3141D509A0F042110249C1CE47
    Server listening on 50895
    Waiting socket
    From Server 50895:tox is connected.
    Waiting socket

这时只要连接到本地的9990端口就好像直接连接到A的3128端口上一样。注意这个连接过程最长可能要一分钟，不过连接成功之后速度就很快了。

指令的格式

    SharpLink local_port target_id target_ip target_port

    local_port  本地端口
    target_id   目标电脑的ID，就是执行服务端程序后显示的那一长串字符
    target_ip   想要目标电脑映射的IP，如果是目标电脑自身，那就是127.0.0.1
    target_port 想要映射的目标端口

实现原理

这个实际上是在本地和远程电脑上分别创建了一个socket，然后这两个socket的通信通过tox来实现。由于tox本身是p2p的所以实现了p2p的效果。从外表看来就好像直接把远程的端口绑定到了本地一样。

## 软件许可
  You are free to do anything...
