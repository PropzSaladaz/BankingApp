﻿using BoneyServer.domain;
using BoneyServer.domain.paxos;
using BoneyServer.services;
using BoneyServer.utils;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace BoneyServer
{

    public class BoneyServerMessageInterceptor : Interceptor {

		public BoneyServerState _state;

		public BoneyServerMessageInterceptor(BoneyServerState state) {
			_state = state;
		}

		public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
			TRequest request,
			ServerCallContext context,
			UnaryServerMethod<TRequest, TResponse> continuation) {
			try {
				if (_state.IsFrozen()) {
					Type requestType = typeof(TRequest);
					Message? _msg = null;

                    if (requestType == typeof(CompareAndSwapReq)) {
						_msg = new Message((CompareAndSwapReq)(object) request, 1);
					}
                    if (requestType == typeof(PrepareReq)) {
                        _msg = new Message((PrepareReq)(object)request, 2);
                    }
                    if (requestType == typeof(AcceptReq)) {
                        _msg = new Message((AcceptReq)(object)request, 3);
                    }

					if (_msg != null) _state.Enqueue(_msg);
					else Logger.LogError("Interceptor: Can't queue message because it does not belong to any of specified types. (l. 39)");
				}

				return await continuation(request, context);

			} catch (Exception ex) {
				Logger.LogError("Interceptor:" + ex.Message + " (l. 45)");
				throw;
			}
		}
	}
	public class BoneyServer
	{

		public static void Main(string[] args) // TODO - edit to receive all server state through the config file
		{
			Logger.DebugOn();
			ServerConfiguration config = ServerConfiguration.ReadConfigFromFile(args[0]);
			uint processID = uint.Parse(args[1]);
			uint maxSlots = (uint)config.GetNumberOfSlots();
			(string hostname, int port) = config.GetBoneyHostnameAndPortByProcess((int)processID);
			
			BoneySlotManager slotManager = new BoneySlotManager(maxSlots);

            IMultiPaxos multiPaxos = new Paxos(processID, maxSlots, config.GetBoneyServersPortsAndAddresses());

            BoneyServerState boneyServerState = new BoneyServerState(processID, multiPaxos,config);
            SlotTimer slotTimer = new SlotTimer(boneyServerState, (uint)config.GetSlotDuration(), config.GetSlotFisrtTime());
            slotTimer.Execute();

            ServerPort serverPort;
            serverPort = new ServerPort(hostname, port, ServerCredentials.Insecure);

			BoneyServerMessageInterceptor _interceptor = new BoneyServerMessageInterceptor(boneyServerState);

            Server server = new Server {
                Services = {
					CompareAndSwapService.BindService(new CompareAndSwapServiceImpl(boneyServerState,multiPaxos)).Intercept(_interceptor),
					PaxosAcceptorService.BindService(new PaxosAcceptorServiceImpl(multiPaxos)).Intercept(_interceptor),
					PaxosLearnerService.BindService(new PaxosLearnerServiceImpl(boneyServerState, multiPaxos)).Intercept(_interceptor)
				        },
                Ports = { serverPort }
            };

            server.Start();

			string startupMessage = $"Started Boney server {processID} at hostname {hostname}:{port}";
			Logger.LogInfo(startupMessage);

			//Configuring HTTP for client connections in Register method
			AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
			while (true) ;

			//server.ShutdownAsync().Wait();
		}

	}

}
