#include <webots/camera.h>
#include <webots/device.h>
#include <webots/display.h>
#include <webots/gps.h>
#include <webots/keyboard.h>
#include <webots/lidar.h>
#include <webots/robot.h>
#include <webots/vehicle/driver.h>

#include <math.h>
#include <stdio.h>
#include <string.h>

#include <sys/socket.h>
#include <netinet/in.h>
#include <arpa/inet.h>
#include <unistd.h>

#include <SDL2/SDL.h>
#include <SDL2/SDL_gamecontroller.h>

// to be used as array indices
enum { X, Y, Z };

#define TIME_STEP 50
#define UNKNOWN 99999.99
#define WAIT_DURATION 100
#define WAIT_STEPS (WAIT_DURATION / TIME_STEP +1)

// Line following PID
#define KP 0.25
#define KI 0.006
#define KD 2

bool PID_need_reset = false;

// Size of the yellow line angle filter
#define FILTER_SIZE 3

// enabe various 'features'
bool enable_collision_avoidance = false;
bool enable_display = false;
bool has_gps = false;
bool has_camera = false;

SDL_GameController *controller = NULL;
int eeg_sock = -1;
int webcam_sock = -1;

// camera
WbDeviceTag camera;
int camera_width = -1;
int camera_height = -1;
double camera_fov = -1.0;

// SICK laser
WbDeviceTag sick;
int sick_width = -1;
double sick_range = -1.0;
double sick_fov = -1.0;

// speedometer
WbDeviceTag display;
int display_width = 0;
int display_height = 0;
WbImageRef speedometer_image = NULL;

// GPS
WbDeviceTag gps;
double gps_coords[3] = {0.0, 0.0, 0.0};
double gps_speed = 0.0;

// misc variables
double speed = 0.0;
double steering_angle = 0.0;
int manual_steering = 0;
bool autodrive = true;

double base_cruise_speed = 50.0; 

void print_help() {
  printf("You can drive this car!\n");
  printf("Select the 3D window and then use the cursor keys to:\n");
  printf("[LEFT]/[RIGHT] - steer\n");
  printf("[UP]/[DOWN] - accelerate/slow down\n");
}

void set_autodrive(bool onoff) {
  if (autodrive == onoff)
    return;
  autodrive = onoff;
  switch (autodrive) {
    case false:
      printf("switching to manual drive...\n");
      printf("hit [A] to return to auto-drive.\n");
      break;
    case true:
      if (has_camera){
        printf("switching to auto-drive... speed = %g \n", speed);
      }  
      else
        printf("impossible to switch auto-drive on without camera...\n");
      break;
  }
}

// set target speed
void set_speed(double kmh) {
  // max speed
  if (kmh > 150.0)
    kmh = 150.0;
  if (kmh < -150.0)
    kmh = -150.0;

  speed = kmh;

  printf("setting speed to %g km/h\n", kmh);
  wbu_driver_set_cruising_speed(kmh);
}

// positive: turn right, negative: turn left
void set_steering_angle(double wheel_angle) {
  // limit the difference with previous steering_angle
  if (wheel_angle - steering_angle > 0.1)
    wheel_angle = steering_angle + 0.1;
  if (wheel_angle - steering_angle < -0.1)
    wheel_angle = steering_angle - 0.1;
  steering_angle = wheel_angle;
  // limit range of the steering angle
  if (wheel_angle > 0.5)
    wheel_angle = 0.5;
  else if (wheel_angle < -0.5)
    wheel_angle = -0.5;
  wbu_driver_set_steering_angle(wheel_angle);
}

void change_manual_steer_angle(int inc) {
  set_autodrive(false);

  double new_manual_steering = manual_steering + inc;
  if (new_manual_steering <= 25.0 && new_manual_steering >= -25.0) {
    manual_steering = new_manual_steering;
    set_steering_angle(manual_steering * 0.02);
  }

  if (manual_steering == 0)
    printf("going straight\n");
  else
    printf("turning %.2f rad (%s)\n", steering_angle, steering_angle < 0 ? "left" : "right");
}

void check_keyboard() {
  int key = wb_keyboard_get_key();
  switch (key) {
    case WB_KEYBOARD_UP:
      set_speed(speed + 5.0);
      break;
    case WB_KEYBOARD_DOWN:
      set_speed(speed - 5.0);
      break;
    case WB_KEYBOARD_RIGHT:
      change_manual_steer_angle(+1);
      break;
    case WB_KEYBOARD_LEFT:
      change_manual_steer_angle(-1);
      break;
    case 'A':
      set_speed(50.0);
      set_autodrive(true);
      break;
    default:
      if (!autodrive){
        wbu_driver_set_steering_angle(0);
        manual_steering = 0;
        
        if (speed >= 5){
          set_speed(speed - 5);
        }
        if (speed < 5 && speed > 0){
          set_speed(0);
        }
        if (speed <= -5 ){
          set_speed(speed + 5);
        }
        if (speed > -5 && speed < 0){
          set_speed(0);
        }
 
      }
      break;
  }
}

void check_gamepad(SDL_GameController *controller) {
  SDL_GameControllerUpdate();

  if (!autodrive && controller) {
    Sint16 steer = SDL_GameControllerGetAxis(controller, SDL_CONTROLLER_AXIS_LEFTX);
    double steer_normalized = steer / 32767.0;
    set_steering_angle(steer_normalized * 0.5);

    Sint16 accel = SDL_GameControllerGetAxis(controller, SDL_CONTROLLER_AXIS_TRIGGERRIGHT);
    if (accel > 1000) set_speed(fmin(speed + 2.0, 100.0));

    Sint16 brake = SDL_GameControllerGetAxis(controller, SDL_CONTROLLER_AXIS_TRIGGERLEFT);
    if (brake > 1000) set_speed(fmax(speed - 2.0, 0.0));

    if (SDL_GameControllerGetButton(controller, SDL_CONTROLLER_BUTTON_A)) {
      set_autodrive(true);
      set_speed(base_cruise_speed);
    }
  }
}

void connect_to_eeg_server() {
  struct sockaddr_in server_addr;

  eeg_sock = socket(AF_INET, SOCK_STREAM, 0);
  if (eeg_sock < 0) {
    perror("socket");
    return;
  }

  server_addr.sin_family = AF_INET;
  server_addr.sin_port = htons(8888);
  inet_pton(AF_INET, "127.0.0.1", &server_addr.sin_addr);

  if (connect(eeg_sock, (struct sockaddr *)&server_addr, sizeof(server_addr)) < 0) {
    perror("connect");
    close(eeg_sock);
    eeg_sock = -1;
  } else {
    printf("Connected to EEG server\n");
  }
}

void read_eeg(int *att, int *med) {
  char buffer[64] = {0};
  if (eeg_sock < 0) return;

  int bytes = recv(eeg_sock, buffer, sizeof(buffer) - 1, MSG_DONTWAIT);
  if (bytes > 0) {
    sscanf(buffer, "%d,%d", att, med);
  }
}

void connect_to_webcam_overlay() {
  struct sockaddr_in addr;
  webcam_sock = socket(AF_INET, SOCK_STREAM, 0);
  if (webcam_sock < 0) return;

  addr.sin_family = AF_INET;
  addr.sin_port = htons(8890);
  inet_pton(AF_INET, "127.0.0.1", &addr.sin_addr);

  printf("Connecting to webcam overlay...\n");
  if (connect(webcam_sock, (struct sockaddr*)&addr, sizeof(addr)) < 0) {
    perror("connect webcam");
    close(webcam_sock);
    webcam_sock = -1;
    return;
  }
  printf("Webcam overlay connected.\n");
}

// compute rgb difference
int color_diff(const unsigned char a[3], const unsigned char b[3]) {
  int i, diff = 0;
  for (i = 0; i < 3; i++) {
    int d = a[i] - b[i];
    diff += d > 0 ? d : -d;
  }
  return diff;
}

// returns approximate angle of yellow road line
// or UNKNOWN if no pixel of yellow line visible
double process_camera_image(const unsigned char *image) {
  int left_sum = 0, right_sum = 0;
  int left_count = 0, right_count = 0;

  int center = camera_width / 2;
  

  for (int y = camera_height -10 ; y < camera_height; y++) {
    for (int x = 0; x < camera_width; x++) {
      //printf("L_sum = %d, R_sum = %d", left_sum, right_sum);
      //printf("L = %d, R = %d", left_count, right_count);
      int index = (y * camera_width + x) * 4;
      const unsigned char *pixel = &image[index];

      int blue = pixel[0], green = pixel[1], red = pixel[2];
      int intensity = (red + green + blue) / 3;
      int max_rgb = fmax(fmax(red, green), blue);
      int min_rgb = fmin(fmin(red, green), blue);
      int saturation = max_rgb - min_rgb;

      // Detect white or light gray with low color variance
      if (intensity > 120 && saturation < 60) {
        if (x < center) {
          left_sum += (x % camera_width);
          left_count++;
        } else {
          right_sum +=  (x % camera_width);
          right_count++;
        }
      }
    }
  }
  
  // if no pixels was detected...
  if (left_count == 0 && right_count == 0)
    return UNKNOWN;
  int lane_center;

  if ( left_count == 0 ) {
    lane_center = right_sum / right_count - ((int)(camera_width / 1.2));
  }
  
  else if ( right_count == 0 ) {
    lane_center = left_sum / left_count + ((int)(camera_width / 1.2));
  }
  
  else {
    lane_center = (left_sum + right_sum) / (left_count + right_count);
  }
  
  int lane_left_bound = camera_width / 3;
int lane_right_bound = 2 * camera_width / 3;

  int region_width = lane_right_bound - lane_left_bound;
double normalized = (double)(lane_center - lane_left_bound) / region_width;
double angle = (normalized - 0.5) * (camera_fov * region_width / camera_width);


  return angle;
      //printf("Pixel (%d, %d): R=%d G=%d B=%d | Intensity=%d Sat=%d\n", x, y, red, green, blue, intensity, saturation);
}
// filter angle of the yellow line (simple average)
double filter_angle(double new_value) {
  static bool first_call = true;
  static double old_value[FILTER_SIZE];
  int i;

  if (first_call || new_value == UNKNOWN) {  // reset all the old values to 0.0
    first_call = false;
    for (i = 0; i < FILTER_SIZE; ++i)
      old_value[i] = 0.0;
  } else {  // shift old values
    for (i = 0; i < FILTER_SIZE - 1; ++i)
      old_value[i] = old_value[i + 1];
  }

  if (new_value == UNKNOWN)
    return UNKNOWN;
  else {
    old_value[FILTER_SIZE - 1] = new_value;
    double sum = 0.0;
    for (i = 0; i < FILTER_SIZE; ++i)
      sum += old_value[i];
    return (double)sum / FILTER_SIZE;
  }
}

// returns approximate angle of obstacle
// or UNKNOWN if no obstacle was detected
double process_sick_data(const float *sick_data, double *obstacle_dist) {
  const int HALF_AREA = 20;  // check 20 degrees wide middle area
  int sumx = 0;
  int collision_count = 0;
  int x;
  *obstacle_dist = 0.0;
  for (x = sick_width / 2 - HALF_AREA; x < sick_width / 2 + HALF_AREA; x++) {
    float range = sick_data[x];
    if (range < 10.0) {
      sumx += x;
      collision_count++;
      *obstacle_dist += range;
    }
  }

  // if no obstacle was detected...
  if (collision_count == 0)
    return UNKNOWN;

  *obstacle_dist = *obstacle_dist / collision_count;
  return ((double)sumx / collision_count / sick_width - 0.5) * sick_fov;
}

void update_display() {
  const double NEEDLE_LENGTH = 50.0;

  // display background
  wb_display_image_paste(display, speedometer_image, 0, 0, false);

  // draw speedometer needle
  double current_speed = wbu_driver_get_current_speed();
  if (isnan(current_speed))
    current_speed = 0.0;
  double alpha = current_speed / 260.0 * 3.72 - 0.27;
  int x = -NEEDLE_LENGTH * cos(alpha);
  int y = -NEEDLE_LENGTH * sin(alpha);
  wb_display_draw_line(display, 100, 95, 100 + x, 95 + y);

  // draw text
  char txt[64];
  sprintf(txt, "GPS coords: %.1f %.1f", gps_coords[X], gps_coords[Z]);
  wb_display_draw_text(display, txt, 10, 130);
  sprintf(txt, "GPS speed:  %.1f", gps_speed);
  wb_display_draw_text(display, txt, 10, 140);
}

void compute_gps_speed() {
  const double *coords = wb_gps_get_values(gps);
  const double speed_ms = wb_gps_get_speed(gps);
  // store into global variables
  gps_speed = speed_ms * 3.6;  // convert from m/s to km/h
  memcpy(gps_coords, coords, sizeof(gps_coords));
}

double applyPID(double yellow_line_angle) {
  static double oldValue = 0.0;
  static double integral = 0.0;

  if (PID_need_reset) {
    oldValue = yellow_line_angle;
    integral = 0.0;
    PID_need_reset = false;
  }

  // anti-windup mechanism
  if (signbit(yellow_line_angle) != signbit(oldValue))
    integral = 0.0;

  double diff = yellow_line_angle - oldValue;

  // limit integral
  if (integral < 30 && integral > -30)
    integral += yellow_line_angle;

  oldValue = yellow_line_angle;
  double base_pid = KP * yellow_line_angle + KI * integral + KD * diff;

  // Compute dynamic steering scale based on speed
  double max_steering_scale = 1.0;  // at 0 km/h
  double min_steering_scale = 0.5;  // at high speed
  double normalized_speed = fmin(speed / 100.0, 1.0);  // cap speed at 100 km/h
  double steering_scale = max_steering_scale - (max_steering_scale - min_steering_scale) * normalized_speed;
  
  return base_pid * steering_scale;
    
  
}

int main(int argc, char **argv) {
  wbu_driver_init();
  connect_to_eeg_server();
  connect_to_webcam_overlay();

  // check if there is a SICK and a display
  int j = 0;
  for (j = 0; j < wb_robot_get_number_of_devices(); ++j) {
    WbDeviceTag device = wb_robot_get_device_by_index(j);
    const char *name = wb_device_get_name(device);
    if (strcmp(name, "Sick LMS 291") == 0)
      enable_collision_avoidance = true;
    else if (strcmp(name, "display") == 0)
      enable_display = true;
    else if (strcmp(name, "gps") == 0)
      has_gps = true;
    else if (strcmp(name, "camera") == 0)
      has_camera = true;
  }

  // camera device
  if (has_camera) {
    camera = wb_robot_get_device("camera");
    wb_camera_enable(camera, TIME_STEP);
    camera_width = wb_camera_get_width(camera);
    camera_height = wb_camera_get_height(camera);
    camera_fov = wb_camera_get_fov(camera);
  }

  // SICK sensor
  if (enable_collision_avoidance) {
    sick = wb_robot_get_device("Sick LMS 291");
    wb_lidar_enable(sick, TIME_STEP);
    sick_width = wb_lidar_get_horizontal_resolution(sick);
    sick_range = wb_lidar_get_max_range(sick);
    sick_fov = wb_lidar_get_fov(sick);
  }

  // initialize gps
  if (has_gps) {
    gps = wb_robot_get_device("gps");
    wb_gps_enable(gps, TIME_STEP);
  }

  // initialize display (speedometer)
  if (enable_display) {
    display = wb_robot_get_device("display");
    speedometer_image = wb_display_image_load(display, "speedometer.png");
  }

  // start engine
  if (has_camera)
    set_speed(50.0);  // km/h
  wbu_driver_set_hazard_flashers(true);
  wbu_driver_set_dipped_beams(true);
  wbu_driver_set_antifog_lights(true);
  wbu_driver_set_wiper_mode(SLOW);

  print_help();

  // allow to switch to manual control
  wb_keyboard_enable(TIME_STEP);
  
  if (SDL_Init(SDL_INIT_GAMECONTROLLER) < 0) {
    fprintf(stderr, "Could not initialize SDL: %s\n", SDL_GetError());
    return 1;
  }
  
  for (int i = 0; i < SDL_NumJoysticks(); ++i) {
    if (SDL_IsGameController(i)) {
      controller = SDL_GameControllerOpen(i);
      if (controller) {
        printf("G29 Steering Wheel connected.\n");
        break;
      }
    }
}

  // main loop
  while (wbu_driver_step() != -1) {
    // get user input
    check_keyboard();
    check_gamepad(controller);

    int attention = -1, meditation = -1;
    read_eeg(&attention, &meditation);
    if (attention >= 0) {
      printf("EEG → Attention: %d, Meditation: %d\n", attention, meditation);
    }

    if (webcam_sock != -1) {
      char buffer[64];
      double sim_time = wb_robot_get_time();
      snprintf(buffer, sizeof(buffer), "sim: %.2f\n", sim_time);
      send(webcam_sock, buffer, strlen(buffer), 0);
    }

    static int i = 0;

    // updates sensors only every TIME_STEP milliseconds
    if (i % (int)(TIME_STEP / wb_robot_get_basic_time_step()) == 0) {
      // read sensors
      const unsigned char *camera_image = NULL;
      const float *sick_data = NULL;
      if (has_camera)
        camera_image = wb_camera_get_image(camera);
      if (enable_collision_avoidance)
        sick_data = wb_lidar_get_range_image(sick);

      if (autodrive && has_camera) {
        double yellow_line_angle = filter_angle(process_camera_image(camera_image));
        
        // Slow down on sharp turns
        double abs_angle = fabs(yellow_line_angle);
        if (yellow_line_angle != UNKNOWN) {
          if (abs_angle > 0.25) {
            // sharp turn — slow down more
            set_speed(fmax(20.0, speed - 1.0));  // decelerate but not below 20
          } else if (abs_angle > 0.1) {
            // moderate curve
            set_speed(fmax(35.0, speed - 0.5));
          } else {
            // straight road — restore cruising speed gradually
            if (speed < base_cruise_speed)
              set_speed(fmin(base_cruise_speed, speed + 0.5));  // restore slowly
          }
        }
        
        static bool line_missing = false;
        if (yellow_line_angle == UNKNOWN) {
          if (!line_missing) {
            fflush(stdout);
            if(enable_display) {
              wb_display_set_color(display, 0xFF0000);
              wb_display_draw_text(display, "Line lost, autodrive off!", 10, 160);
            }
            line_missing = true;
            
            set_speed(10.0);
            set_steering_angle(0.0);
            printf("Line lost. Autodrive off...");
            set_autodrive(false);
          }
        } else {
          if (line_missing) {
            fflush(stdout);
            if(enable_display) {
              wb_display_set_color(display, 0x00AA00);
              wb_display_draw_text(display, "Line found, to turn autodrive on press 'A'.", 10, 160);
            }
            line_missing = false;
          }
        }
        
        double obstacle_dist;
        double obstacle_angle;
        if (enable_collision_avoidance){
          obstacle_angle = process_sick_data(sick_data, &obstacle_dist);
          
        } else {
          obstacle_angle = UNKNOWN;
          obstacle_dist = 0;
        }

        // avoid obstacles and follow yellow line
        if (enable_collision_avoidance && obstacle_angle != UNKNOWN) {
          // an obstacle has been detected
          wbu_driver_set_brake_intensity(0.0);
          // compute the steering angle required to avoid the obstacle
          double obstacle_steering = steering_angle;
          if (obstacle_angle > 0.0 && obstacle_angle < 0.4)
            obstacle_steering = steering_angle + (obstacle_angle - 0.25) / obstacle_dist;
          else if (obstacle_angle > -0.4)
            obstacle_steering = steering_angle + (obstacle_angle + 0.25) / obstacle_dist;
          double steer = steering_angle;
          // if we see the line we determine the best steering angle to both avoid obstacle and follow the line
          if (yellow_line_angle != UNKNOWN) {
            const double line_following_steering = applyPID(yellow_line_angle);
            if (obstacle_steering > 0 && line_following_steering > 0)
              steer = obstacle_steering > line_following_steering ? obstacle_steering : line_following_steering;
            else if (obstacle_steering < 0 && line_following_steering < 0)
              steer = obstacle_steering < line_following_steering ? obstacle_steering : line_following_steering;
          } else
            PID_need_reset = true;
          // apply the computed required angle
          set_steering_angle(steer);
        } else if (yellow_line_angle != UNKNOWN) {
          // no obstacle has been detected, simply follow the line
          wbu_driver_set_brake_intensity(0.0);
          set_steering_angle(applyPID(yellow_line_angle));
        } else {
          // no obstacle has been detected but we lost the line => we brake and hope to find the line again
          wbu_driver_set_brake_intensity(0.4);
          PID_need_reset = true;
        }
      }

      // update stuff
      if (has_gps)
        compute_gps_speed();
      if (enable_display)
        update_display();
    }

    ++i;
  }

  wbu_driver_cleanup();
  
  if (controller)
    SDL_GameControllerClose(controller);
  SDL_Quit();

  return 0;  // ignored
}
