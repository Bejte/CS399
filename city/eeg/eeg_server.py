# eeg/eeg_server.py

import asyncio
import socket
from bleak import BleakClient, BleakScanner

# TODO: UUID for data — placeholder, update if needed
EEG_UUID = "8667556c-9a37-4c91-84ed-54ee27d90049"

PORT = 8888

def decode_payload(data):
    # TODO: Adjust this function based on the actual data format
    if len(data) >= 4 and data[0] == 0x04 and data[1] == 0x04:
        att = data[2]
        med = data[3]
        return att, med
    return None, None

async def run_ble_and_stream(address):
    async with BleakClient(address) as client:
        print("Connected to MindWave.")
        
        # TCP socket server
        server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        server.bind(('localhost', PORT))
        server.listen(1)
        print("Waiting for Webots connection on port", PORT)
        conn, addr = server.accept()
        print(f"Webots connected from {addr}")

        #def handler(_, data):
        #    att, med = decode_payload(data)
        #    if att is not None:
        #        message = f"{att},{med}\n"
        #        print(f"Sending: {message.strip()}")
        #        try:
        #            conn.sendall(message.encode())
        #        except:
        #            print("Webots disconnected.")
        #            conn.close()
        
        def handler(_, data):
            print(f"Raw data: {list(data)}")  # Just show what comes in


        await client.start_notify(EEG_UUID, handler)
        try:
            await asyncio.sleep(300)  # Run for 5 mins
        finally:
            await client.stop_notify(EEG_UUID)
            conn.close()
            server.close()

async def main():
    TARGET_ADDRESS = "47109842-B76E-43FA-4C0A-7BFAAD477705"

    print("Scanning for BLE devices...")
    devices = await BleakScanner.discover(timeout=10.0)

    for d in devices:
        print(f"Discovered: {d.name} @ {d.address}")
        if d.address == TARGET_ADDRESS:
            print(f"Trying to connect to MindWave @ {TARGET_ADDRESS}...")
            await run_ble_and_stream(d.address)
            return

    print("MindWave not found.")

if __name__ == "__main__":
    asyncio.run(main())
