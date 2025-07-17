# eeg/mindwave_logger.py

import asyncio
from bleak import BleakClient, BleakScanner
import datetime
import csv

# TODO: Replace this with the correct UUIDs (varies by firmware)
ATTENTION_UUID = "0000ffe1-0000-1000-8000-00805f9b34fb"

CSV_LOG_PATH = "eeg_log.csv"

def decode_payload(data):
    # TODO: Adjust this function based on the actual data format
    if data[0] == 0x04 and data[1] == 0x04:
        attention = data[2]
        meditation = data[3]
        return attention, meditation
    return None, None

async def run(address):
    async with BleakClient(address) as client:
        print("Connected to MindWave Mobile 2")

        with open(CSV_LOG_PATH, "w", newline="") as csvfile:
            writer = csv.writer(csvfile)
            writer.writerow(["timestamp", "attention", "meditation"])

            def notification_handler(sender, data):
                timestamp = datetime.datetime.now().isoformat()
                att, med = decode_payload(data)
                if att is not None and med is not None:
                    print(f"[{timestamp}] Attention: {att}, Meditation: {med}")
                    writer.writerow([timestamp, att, med])
                    csvfile.flush()

            await client.start_notify(ATTENTION_UUID, notification_handler)
            await asyncio.sleep(300)  # log for 5 mins
            await client.stop_notify(ATTENTION_UUID)

# Discover devices and start
async def main():
    print("Scanning for MindWave Mobile 2...")
    devices = await BleakScanner.discover()
    for d in devices:
        if "MindWave" in d.name:
            await run(d.address)
            return
    print("MindWave not found.")

if __name__ == "__main__":
    asyncio.run(main())
