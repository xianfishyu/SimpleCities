shader_type canvas_item;
render_mode unshaded;

// ── 背景 ──
uniform vec3 background_color = vec3(1.0, 1.0, 1.0);

// ── 主网格 ──
uniform float major_grid_size = 500.0;
uniform float major_line_width = 2.0;
uniform vec3 major_grid_color = vec3(0.85, 0.85, 0.85);

// ── 次网格 ──
uniform float minor_grid_size = 100.0;
uniform float minor_line_width = 0.5;
uniform vec3 minor_grid_color = vec3(0.92, 0.92, 0.92);

// ── 点网格 ──
uniform float dot_grid_size = 10.0;
uniform float dot_radius = 0.5;
uniform vec3 dot_color = vec3(0.75, 0.75, 0.75);

// ── 显示开关 ──
uniform bool show_major_grid = true;
uniform bool show_minor_grid = true;
uniform bool show_dot_grid = true;

// ── 相机 ──
uniform vec2 camera_pos = vec2(0.0);
uniform float camera_zoom = 1.0;
uniform vec2 viewport_size = vec2(1920.0, 1080.0);

void fragment() {
    vec2 world_pos = (UV - vec2(0.5)) * viewport_size / camera_zoom + camera_pos;
    vec2 deriv = fwidth(world_pos);
    float aa = max(deriv.x, deriv.y);

    vec4 final_color = vec4(background_color, 1.0);

    // ── 点网格（网格线上不渲染）──
    if (show_dot_grid && dot_grid_size > 0.0) {
        vec2 dot_step = vec2(dot_grid_size);
        vec2 dot_pos = mod(world_pos + dot_step * 0.5, dot_step);
        float dist_to_dot = length(dot_pos - dot_step * 0.5);
        float dot_alpha = clamp(1.0 - smoothstep(dot_radius - aa, dot_radius + aa, dist_to_dot), 0.0, 1.0);

        // 次网格线上不绘点
        if (show_minor_grid && minor_grid_size > 0.0) {
            vec2 mp = mod(world_pos, minor_grid_size);
            float dmx = min(mp.x, minor_grid_size - mp.x);
            float dmy = min(mp.y, minor_grid_size - mp.y);
            if (min(dmx, dmy) <= minor_line_width * 0.5 + aa) dot_alpha = 0.0;
        }
        // 主网格线上不绘点
        if (show_major_grid && major_grid_size > 0.0) {
            vec2 mp = mod(world_pos, major_grid_size);
            float dmx = min(mp.x, major_grid_size - mp.x);
            float dmy = min(mp.y, major_grid_size - mp.y);
            if (min(dmx, dmy) <= major_line_width * 0.5 + aa) dot_alpha = 0.0;
        }

        if (dot_alpha > 0.0) {
            final_color = mix(final_color, vec4(dot_color, 1.0), 0.5 * dot_alpha);
        }
    }

    // ── 次网格线（浅灰，细线）──
    if (show_minor_grid && minor_grid_size > 0.0) {
        vec2 minor_pos = mod(world_pos, minor_grid_size);
        float dx = min(minor_pos.x, minor_grid_size - minor_pos.x);
        float dy = min(minor_pos.y, minor_grid_size - minor_pos.y);
        float dist = min(dx, dy);
        float halfw = minor_line_width * 0.5;
        float alpha = clamp(1.0 - smoothstep(halfw - aa, halfw + aa, dist), 0.0, 1.0);
        if (alpha > 0.0) {
            final_color = mix(final_color, vec4(minor_grid_color, 1.0), alpha);
        }
    }

    // ── 主网格线（深灰，粗线）──
    if (show_major_grid && major_grid_size > 0.0) {
        vec2 major_pos = mod(world_pos, major_grid_size);
        float dx = min(major_pos.x, major_grid_size - major_pos.x);
        float dy = min(major_pos.y, major_grid_size - major_pos.y);
        float dist = min(dx, dy);
        float halfw = major_line_width * 0.5;
        float alpha = clamp(1.0 - smoothstep(halfw - aa, halfw + aa, dist), 0.0, 1.0);
        if (alpha > 0.0) {
            final_color = mix(final_color, vec4(major_grid_color, 1.0), alpha);
        }
    }

    COLOR = final_color;
}
