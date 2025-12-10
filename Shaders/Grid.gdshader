shader_type canvas_item;
render_mode unshaded;

// 主网格参数
uniform float major_grid_size = 500.0;
uniform float major_line_width = 2.0;
uniform vec3 major_grid_color = vec3(0.3, 0.3, 0.3);

// 次网格参数
uniform float minor_grid_size = 100.0;
uniform float minor_line_width = 1.0;
uniform vec3 minor_grid_color = vec3(0.2, 0.2, 0.2);

// 点网格参数
uniform float dot_grid_size = 10.0;
uniform float dot_radius = 0.5;
uniform vec3 dot_color = vec3(0.6, 0.6, 0.6);

// 显示开关
uniform bool show_background = true;
uniform bool show_major_grid = true;
uniform bool show_minor_grid = true;
uniform bool show_dot_grid = true;
uniform vec3 background_color = vec3(0.5, 0.5, 0.5);

// 相机参数
uniform vec2 camera_pos = vec2(0.0);
uniform float camera_zoom = 1.0;
uniform vec2 viewport_size = vec2(1920.0, 1080.0);

void fragment() {
    vec4 final_color = vec4(background_color, 1.0);

    if (!show_background) {
        final_color = vec4(0.0, 0.0, 0.0, 0.0);
    }

    // 计算世界坐标（使用 UV 坐标从 0.0-1.0 转换到屏幕像素）
    vec2 world_pos = (UV - vec2(0.5)) * viewport_size / camera_zoom + camera_pos;

    // 计算屏幕/片段导数，用于抗锯齿宽度估算
    vec2 deriv = fwidth(world_pos);
    float aa = max(deriv.x, deriv.y);

    // 点网格（带抗锯齿）
    if (show_dot_grid && dot_grid_size > 0.0) {
        vec2 dot_grid_step = vec2(dot_grid_size );
        vec2 dot_pos = mod(world_pos + dot_grid_step * 0.5, dot_grid_step);
        float dist_to_dot = length(dot_pos - dot_grid_step * 0.5);

        float dot_alpha = clamp(1.0 - smoothstep(dot_radius - aa, dot_radius + aa, dist_to_dot), 0.0, 1.0);
        if (dot_alpha > 0.0) {
            final_color = mix(final_color, vec4(dot_color, 1.0), 0.8 * dot_alpha);
        }
    }

    // 次网格线（带抗锯齿）
    if (show_minor_grid && minor_grid_size > 0.0) {
        vec2 minor_pos = mod(world_pos, minor_grid_size);
        float dist_to_line_x = min(minor_pos.x, minor_grid_size - minor_pos.x);
        float dist_to_line_y = min(minor_pos.y, minor_grid_size - minor_pos.y);
        float dist_line = min(dist_to_line_x, dist_to_line_y);

        float halfw = minor_line_width * 0.5;
        float line_alpha = clamp(1.0 - smoothstep(halfw - aa, halfw + aa, dist_line), 0.0, 1.0);
        if (line_alpha > 0.0) {
            final_color = mix(final_color, vec4(minor_grid_color, 1.0), 0.6 * line_alpha);
        }
    }

    // 主网格线（带抗锯齿）
    if (show_major_grid && major_grid_size > 0.0) {
        vec2 major_pos = mod(world_pos, major_grid_size);
        float dist_to_line_x = min(major_pos.x, major_grid_size - major_pos.x);
        float dist_to_line_y = min(major_pos.y, major_grid_size - major_pos.y);
        float dist_line = min(dist_to_line_x, dist_to_line_y);

        float halfwM = major_line_width * 0.5;
        float line_alphaM = clamp(1.0 - smoothstep(halfwM - aa, halfwM + aa, dist_line), 0.0, 1.0);
        if (line_alphaM > 0.0) {
            final_color = mix(final_color, vec4(major_grid_color, 1.0), 1.0 * line_alphaM);
        }
    }

    COLOR = final_color;
}
