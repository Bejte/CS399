import cv2
import socket
import threading
import time
import csv

# Shared state
sim_time = "sim: --.--"
attention = "-"
steering = "-"
running = True

# Create CSV logger
log_file = open("video_log.csv", "w", newline='')
logger = csv.writer(log_file)
logger.writerow(["frame_unix_time", "sim_time", "attention", "steering"])

# Socket listener for Webots
def sim_data_listener():
    global sim_time, attention, steering, running
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        s.bind(('localhost', 8890))
        s.listen(1)
        print("[Webcam] Waiting for Webots on port 8890...")
        conn, _ = s.accept()
        print("[Webcam] Connected to Webots")
        with conn:
            while running:
                try:
                    data = conn.recv(64)
                    if not data:
                        break
                    msg = data.decode().strip()  # e.g., "sim: 12.34, att: 70, steer: 0.20"
                    parts = msg.split(',')
                    for part in parts:
                        if "sim:" in part:
                            sim_time = part.strip()
                        elif "att:" in part:
                            attention = part.strip().split(':')[1].strip()
                        elif "steer:" in part:
                            steering = part.strip().split(':')[1].strip()
                except:
                    break

# Start the listener in a thread
threading.Thread(target=sim_data_listener, daemon=True).start()

# Start webcam
cap = cv2.VideoCapture(0)
if not cap.isOpened():
    print("[ERROR] Webcam not accessible.")
    exit()

# Configure video writer
fourcc = cv2.VideoWriter_fourcc(*'XVID')
out = cv2.VideoWriter("webcam_synced.avi", fourcc, 20.0, (640, 480))

print("[Webcam] Recording started. Press 'q' to quit.")

try:
    while True:
        ret, frame = cap.read()
        if not ret:
            print("[ERROR] Failed to read frame")
            break

        # Timestamp and overlays
        now = time.time()
        overlay_time = time.strftime('%Y-%m-%d %H:%M:%S', time.localtime(now))
        overlay_sim = sim_time
        overlay_att = f"att: {attention}"
        overlay_steer = f"steer: {steering}"

        # Draw overlays
        cv2.putText(frame, overlay_time, (10, 30),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255, 255, 255), 2)
        cv2.putText(frame, overlay_sim, (10, 60),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 255, 0), 2)
        cv2.putText(frame, overlay_att, (10, 90),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 200, 255), 2)
        cv2.putText(frame, overlay_steer, (10, 120),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255, 200, 0), 2)

        # Log to CSV
        logger.writerow([now, sim_time.replace("sim:", "").strip(), attention, steering])

        out.write(frame)
        cv2.imshow("Webcam", frame)
        if cv2.waitKey(1) & 0xFF == ord('q'):
            break

except KeyboardInterrupt:
    print("\n[Webcam] Interrupted.")

finally:
    running = False
    cap.release()
    out.release()
    cv2.destroyAllWindows()
    log_file.close()
    print("[Webcam] Recording finished. Video and log saved.")
