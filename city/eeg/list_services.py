import asyncio
from bleak import BleakClient

TARGET_ADDRESS = "47109842-B76E-43FA-4C0A-7BFAAD477705"

async def main():
    async with BleakClient(TARGET_ADDRESS) as client:
        print(f"Connected to {TARGET_ADDRESS}")
        await client.connect()
        for service in client.services:
            print(f"[Service] {service.uuid}: {service.description}")
            for char in service.characteristics:
                print(f"  [Characteristic] {char.uuid} — {char.properties}")

asyncio.run(main())
