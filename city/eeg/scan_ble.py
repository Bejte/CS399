# scan_ble.py
import asyncio
from bleak import BleakScanner

async def main():
    print("Scanning for BLE devices...")
    devices = await BleakScanner.discover(timeout=10.0)
    for d in devices:
        print(f"Name: {d.name}, Address: {d.address}")

asyncio.run(main())
