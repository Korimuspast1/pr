namespace FirstPerson.Net
{
	internal interface INetEvents
	{
		void LinkUp(NetLink link);

		void LinkDown(NetLink link, string reason);

		void Data(NetLink link, byte[] data, int off, int len);
	}
}
