﻿/****************************************************
	文件：LoginSys.cs
	作者：朱龙飞
	邮箱: 398670608@qq.com
	日期：2019/07/08 10:32   	
	功能：登录系统管业务
*****************************************************/


using PEProtocol;

public class LoginSys
{
    private static LoginSys instance = null;
    public static LoginSys Instance {
        get {
            if (instance == null)
            {
                instance = new LoginSys();
            }
            return instance;
        }
    }
    private CacheSvc cacheSvc = null;
    private static TimerSvc timerSvc = null;

    public void Init()
    {
        cacheSvc = CacheSvc.Instance;
        timerSvc = TimerSvc.Instance;
        PECommon.Log("LoginSys Init Done.");
    }

    public void ReqLogin(MsgPack pack)
    {
        ReqLogin data = pack.msg.reqLogin;

        //当前账号是否已经上线
        GameMsg msg = new GameMsg
        {
            cmd = (int)CMD.RspLogin,
        };

        if (cacheSvc.IsAcctOnLine(data.acct))
        {
            //已上线：返回错误信息
            msg.err = (int)ErrorCode.AcctIsOnLine;
        }
        else
        {
            //未上线：
            //账号是否存在
            PlayerData pd = cacheSvc.GetPlayerData(data.acct, data.pass);
            if (pd == null)
            {
                //不存在，密码错误
                msg.err = (int)ErrorCode.WrongPass;
            }
            else
            {
                #region 计算离线体力增长
                int power = pd.power;
                long now = timerSvc.GetNowTime();
                long milliseconds = now - pd.time;
                int addpower = (int)(milliseconds / (1000 * 60 * PECommon.PowerAddSpace)) * PECommon.PowerAddCount;
                if (addpower > 0)
                {
                    int powerMax = PECommon.GetPowerLimit(pd.lv);
                    if (pd.power < powerMax)
                    {
                        pd.power += addpower;
                        if (pd.power > powerMax)
                        {
                            pd.power = powerMax;
                        }
                    }
                }

                if (power != pd.power)
                {
                    cacheSvc.UpdatePlayerData(pd.id, pd);
                }
                #endregion

                msg.rspLogin = new RspLogin
                {
                    playerData = pd
                };
                //缓存账号数据
                cacheSvc.AcctOnline(data.acct, pack.session, pd);
            }

        }

        //回应客户端

        pack.session.SendMsg(msg);
    }

    public void ReqRename(MsgPack pack)
    {
        ReqRename data = pack.msg.reqRename;
        GameMsg msg = new GameMsg
        {
            cmd = (int)CMD.RspRename
        };

        if (cacheSvc.IsNameExist(data.name))
        {
            //名字是否已经存在
            //存在：返回错误码
            msg.err = (int)ErrorCode.NameIsExist;
        }
        else
        {
            //不存在：更新缓存，以及数据库，再返回给客户端
            PlayerData playerData = cacheSvc.GetPlayerDataBySession(pack.session);
            playerData.name = data.name;

            if (!cacheSvc.UpdatePlayerData(playerData.id, playerData))
            {
                msg.err = (int)ErrorCode.UpdateDBError;
            }
            else
            {
                msg.rspRename = new RspRename
                {
                    name = data.name
                };
            }
        }

        pack.session.SendMsg(msg);
    }

    public void ClearOffLineData(ServerSession session)
    {
        //写入下线时间
        PlayerData pd = cacheSvc.GetPlayerDataBySession(session);
        if (pd != null)
        {
            pd.time = timerSvc.GetNowTime();
            if (!cacheSvc.UpdatePlayerData(pd.id, pd))
            {
                PECommon.Log("Update offline time error", LogType.Error);
            }
            cacheSvc.AcctOffLine(session);
        }
    }
}